using Amazon.CDK;
using Amazon.CDK.AWS.AutoScaling;
using Amazon.CDK.AWS.EC2;
using Constructs;

namespace TaskCdk
{
    public class TaskCdkStack : Stack
    {
        internal TaskCdkStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            Vpc vpc = new Vpc(this, "VPC", new VpcProps {
                IpAddresses = IpAddresses.Cidr("10.0.1.0/20"),
                MaxAzs = 3,
                //SubnetConfiguration = new[] { new SubnetConfiguration {
                //}, new SubnetConfiguration {
                //    CidrMask = 24,
                //    Name = "Application",
                //    SubnetType = SubnetType.PUBLIC
                //}, new SubnetConfiguration {
                //    CidrMask = 28,
                //    Name = "Database",
                //    SubnetType = SubnetType.PRIVATE_ISOLATED,

                //    // 'reserved' can be used to reserve IP address space. No resources will
                //    // be created for this subnet, but the IP range will be kept available for
                //    // future creation of this subnet, or even for future subdivision.
                //    Reserved = true
                //}}
            });

            SecurityGroup mySecurityGroup = new SecurityGroup(this, "SecurityGroup", new SecurityGroupProps { Vpc = vpc });


            AutoScalingGroup autoScalingGroup =  new AutoScalingGroup(this, "ASG", new AutoScalingGroupProps
            {
                Vpc = vpc,
                InstanceType = InstanceType.Of(InstanceClass.BURSTABLE2, InstanceSize.MICRO),
                MachineImage = new AmazonLinuxImage(),
                SecurityGroup = mySecurityGroup
            });


        }
    }
}
