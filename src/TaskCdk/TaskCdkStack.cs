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
                IpAddresses = IpAddresses.Cidr("10.0.0.0/16"),
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
                },
                new SubnetConfiguration
                {
                    Name = "Bastion",
                    CidrMask = 24,
                    SubnetType = SubnetType.PUBLIC,
                }
            }

            });

            //sim eu sei das questões de segurança,
            //pode permitir que os recursos dentro da VPC sejam acessados de forma não autorizada pela Internet
            ((Subnet)vpc.PrivateSubnets[0]).AddRoute("AppRoute", new AddRouteOptions
            {
                RouterId = vpc.InternetGatewayId,
                RouterType = RouterType.GATEWAY,
                DestinationCidrBlock = "0.0.0.0/0"
            });

            ((Subnet)vpc.PrivateSubnets[1]).AddRoute("DatabaseRoute", new AddRouteOptions
            {
                RouterId = vpc.InternetGatewayId,
                RouterType = RouterType.GATEWAY,
                DestinationCidrBlock = "0.0.0.0/0"
            });

            ((Subnet)vpc.PublicSubnets[0]).AddRoute("BastionRoute", new AddRouteOptions
            {
                RouterId = vpc.InternetGatewayId,
                RouterType = RouterType.GATEWAY,
                DestinationCidrBlock = "0.0.0.0/0"
            });
  

            SecurityGroup SecurityGroup = new SecurityGroup(this, "MySecurityGroup", new SecurityGroupProps 
            {
                Vpc = vpc, 
                SecurityGroupName = "SecurityGroup",
                AllowAllOutbound= true
            });

            //restringir o acesso a essas portas somente aos IPs necessários ou a um grupo específico de IPs confiáveis.
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

            cluster.AddAutoScalingGroupCapacity("app-nodes", new AutoScalingGroupCapacityOptions
            {
                InstanceType = new InstanceType("t2.micro"),
                MinCapacity = 2,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS }
            });

            new DatabaseInstance(this, "DB", new DatabaseInstanceProps
            {
                Vpc = vpc,
                Port = 1433,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_WITH_EGRESS },
                InstanceType = new InstanceType("t2.micro"),
                InstanceIdentifier = "DBInstance",
                Engine = DatabaseInstanceEngine.Mysql( new MySqlInstanceEngineProps
                {
                    Version = MysqlEngineVersion.VER_8_0_30
                })
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
                SubnetSelection = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                InstanceType = new InstanceType("t2.micro"),
                MachineImage = new AmazonLinuxImage(),
                SecurityGroup = bastionSecurityGroup,
            });

        }
    }
}
