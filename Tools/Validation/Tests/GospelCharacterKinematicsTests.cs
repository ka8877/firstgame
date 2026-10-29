using System;
using NUnit.Framework;
using PathOfDawn.Core;
using K=PathOfDawn.Core.GospelCharacterKinematics;

public class GospelCharacterKinematicsTests
{
    [TestCase(.3f,-.6f)][TestCase(-.3f,-.6f)][TestCase(0f,-1f)][TestCase(0f,0f)]
    public void ReachableAndClampedTargetsPreserveBothRealBoneLengths(float x,float y)
    {
        var chain=K.Solve(new K.Point(0,0),new K.Point(x,y),.38f,.37f,1);
        Assert.That((chain.Joint-chain.Root).Length,Is.EqualTo(.38f).Within(.00002));
        Assert.That((chain.Tip-chain.Joint).Length,Is.EqualTo(.37f).Within(.00002));
        Assert.That(chain.Joint.X-chain.Root.X,Is.GreaterThan(-.3f));
    }
    [Test]
    public void RestSkinIsIdentityAcrossElbowAndAnkleBlendBands()
    {
        var root=new K.SkinTransform(new K.Point(0,0),-90,new K.Point(0,0),-90);
        var joint=new K.SkinTransform(new K.Point(0,-.38f),-90,new K.Point(0,-.38f),-90);
        var tip=new K.SkinTransform(new K.Point(0,-.75f),0,new K.Point(0,-.75f),0);
        for(int i=0;i<=100;i++)
        {
            var p=new K.Point(.04f,-i*.008f);
            var q=K.Skin(p,K.BindWeights(-p.Y,.38f,.75f),root,joint,tip);
            Assert.That(q.X,Is.EqualTo(p.X).Within(.000001));Assert.That(q.Y,Is.EqualTo(p.Y).Within(.000001));
        }
    }
    [Test]
    public void BentWholeLimbStaysContinuousThroughJointInsteadOfSeparatingCutouts()
    {
        var chain=K.Solve(new K.Point(0,0),new K.Point(.2f,-.35f),.24f,.24f,-1);
        var upper=new K.SkinTransform(new K.Point(0,0),-90,chain.Root,chain.UpperDegrees);
        var lower=new K.SkinTransform(new K.Point(0,-.24f),-90,chain.Joint,chain.LowerDegrees);
        var hand=new K.SkinTransform(new K.Point(0,-.48f),-90,chain.Tip,chain.LowerDegrees);
        K.Point previous=default;
        for(int i=0;i<=100;i++)
        {
            var p=new K.Point(.02f,-.19f-i*.001f);
            var q=K.Skin(p,K.BindWeights(-p.Y,.24f,.48f,.045f),upper,lower,hand);
            if(i>0)Assert.That((q-previous).Length,Is.LessThan(.004f));
            previous=q;
        }
    }
    [Test]
    public void StanceFootRemainsOnGroundWhileSwingFootClearsIt()
    {
        float previous=float.PositiveInfinity;
        for(int i=0;i<58;i++)
        {
            var p=K.Foot(i/100f,.3f,.58f,.1f,.12f,out float pitch);
            Assert.That(p.Y,Is.EqualTo(.1f).Within(.00001));Assert.That(p.X,Is.LessThan(previous));previous=p.X;
        }
        for(int i=59;i<100;i++)Assert.That(K.Foot(i/100f,.3f,.58f,.1f,.12f,out _).Y,Is.GreaterThan(.1f));
    }
}
