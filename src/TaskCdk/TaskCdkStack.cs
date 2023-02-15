using Amazon.CDK;
using Amazon.CDK.AWS.AutoScaling;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ElasticLoadBalancingV2;
using Amazon.CDK.AWS.EKS;
using Constructs;
using Amazon.CDK.AWS.RDS;

namespace TaskCdk
{
    public class TaskCdkStack : Stack
    {
        internal TaskCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            Vpc vpc = new Vpc(this, "VPC", new VpcProps {
                MaxAzs = 2,
                SubnetConfiguration = new SubnetConfiguration[] {
                new SubnetConfiguration
                {
                    Name = "Application",
                    CidrMask = 24,
                    SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
                },
                new SubnetConfiguration
                {
                    Name = "Database",
                    CidrMask = 28,
                    SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
                }
            }
            });


            SecurityGroup SecurityGroup = new SecurityGroup(this, "SecurityGroup", new SecurityGroupProps 
            {
                Vpc = vpc, 
                SecurityGroupName = "SecurityGroup",
                AllowAllOutbound= true
            });

            SecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(80), "Allow HTTP access");
            SecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(443), "Allow HTTPS access");


            AutoScalingGroup autoScalingGroup =  new AutoScalingGroup(this, "ASG", new AutoScalingGroupProps
            {
                Vpc = vpc,
                InstanceType = InstanceType.Of(InstanceClass.T2, InstanceSize.MICRO),
                MachineImage = new AmazonLinuxImage(),
                SecurityGroup = SecurityGroup
            });

            ApplicationLoadBalancer lb = new ApplicationLoadBalancer(this, "LB", new ApplicationLoadBalancerProps 
            {
                Vpc = vpc,
                SecurityGroup = SecurityGroup,
                InternetFacing = true,
                LoadBalancerName = "AppLoadBalancer",
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS }
            });

            ApplicationListener applicationListener = lb.AddListener("Listener", new BaseApplicationListenerProps
            {
                Port = 80,
                Open = true
            });

            applicationListener.AddTargets("ApplicationFleet", new AddApplicationTargetsProps
            {
                Port = 8080,
                Targets = new [] { autoScalingGroup }
            });

            Cluster cluster = new Cluster(this, "EKS", new ClusterProps { 
               Version = KubernetesVersion.V1_21,
            });

            cluster.AddAutoScalingGroupCapacity("frontend-nodes", new AutoScalingGroupCapacityOptions
            {
                InstanceType = new InstanceType("t2.micro"),
                MinCapacity = 2,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS }
            });


            new DatabaseCluster(this, "Database", new DatabaseClusterProps
            {
                Engine = DatabaseClusterEngine.AuroraMysql(new AuroraMysqlClusterEngineProps
                {
                    Version = AuroraMysqlEngineVersion.VER_2_09_2
                }),

                InstanceProps = new Amazon.CDK.AWS.RDS.InstanceProps
                {
                    InstanceType = new InstanceType("t2.micro"),
                    VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS, SubnetGroupName = "Database" },
                    Vpc = vpc
                },

                DefaultDatabaseName = "database",
            });

            var bastionSecurityGroup = new SecurityGroup(this, "BastionSecurityGroup", new SecurityGroupProps
            {
                Vpc = vpc,
                SecurityGroupName = "Bastion Security Group",
                AllowAllOutbound = true
            });

            bastionSecurityGroup.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(22), "Allow SSH access from anywhere");

            new BastionHostLinux(this, "BastionHost", new BastionHostLinuxProps 
            { 
                Vpc = vpc,
                SubnetSelection = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS },
                InstanceType = new InstanceType("t2.micro"),
                MachineImage = new AmazonLinuxImage(),
                SecurityGroup = bastionSecurityGroup,
            });

        }
    }
}
