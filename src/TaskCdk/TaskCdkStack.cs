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
                IpAddresses = IpAddresses.Cidr("10.0.1.0/16"),
                MaxAzs = 3,
                SubnetConfiguration = new[] { new SubnetConfiguration 
                {
                    }, new SubnetConfiguration {
                        CidrMask = 24,
                        Name = "Ingress",
                        SubnetType = SubnetType.PUBLIC,
                    },new SubnetConfiguration {
                        CidrMask = 24,
                        Name = "Application",
                        SubnetType = SubnetType.PRIVATE_WITH_EGRESS,
                    }, new SubnetConfiguration {
                        CidrMask = 28,
                        Name = "Database",
                        SubnetType = SubnetType.PRIVATE_ISOLATED,
                    }
                }
            });

            SecurityGroup mySecurityGroup = new SecurityGroup(this, "SecurityGroup", new SecurityGroupProps 
            {
                Vpc = vpc, 
                SecurityGroupName = "SecurityGroup",
                AllowAllOutbound= true
            });


            AutoScalingGroup autoScalingGroup =  new AutoScalingGroup(this, "ASG", new AutoScalingGroupProps
            {
                Vpc = vpc,
                InstanceType = InstanceType.Of(InstanceClass.T2, InstanceSize.MICRO),
                MachineImage = new AmazonLinuxImage(),
                SecurityGroup = mySecurityGroup
            });

            // aplication load balancer, EKS, Bastion e Banco (RDS ou Aurora)

            ApplicationLoadBalancer lb = new ApplicationLoadBalancer(this, "LB", new ApplicationLoadBalancerProps 
            {
                Vpc = vpc,
                SecurityGroup = mySecurityGroup,
                InternetFacing = true,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC }
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
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC }
            });


            DatabaseCluster databaseCluster = new DatabaseCluster(this, "Database", new DatabaseClusterProps
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
                
            });


            BastionHostLinux bastionHostLinux = new BastionHostLinux(this, "BastionHost", new BastionHostLinuxProps 
            { 
                Vpc = vpc,
            });


        }
    }
}
