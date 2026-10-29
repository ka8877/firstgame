using System;

namespace PathOfDawn.Core
{
    /// <summary>Pure character geometry shared by the actual painted rig and regression tests.</summary>
    public static class GospelCharacterKinematics
    {
        public readonly struct Point
        {
            public readonly float X, Y;
            public Point(float x, float y) { X=x;Y=y; }
            public static Point operator +(Point a, Point b) => new Point(a.X+b.X,a.Y+b.Y);
            public static Point operator -(Point a, Point b) => new Point(a.X-b.X,a.Y-b.Y);
            public static Point operator *(Point a, float b) => new Point(a.X*b,a.Y*b);
            public float Length => (float)Math.Sqrt(X*X+Y*Y);
        }
        public readonly struct Chain
        {
            public readonly Point Root, Joint, Tip;
            public readonly float UpperDegrees, LowerDegrees;
            public Chain(Point root,Point joint,Point tip,float upper,float lower)
            {Root=root;Joint=joint;Tip=tip;UpperDegrees=upper;LowerDegrees=lower;}
        }
        public readonly struct SkinTransform
        {
            readonly float cosine,sine;
            readonly Point translation;
            public SkinTransform(Point bindOrigin,float bindDegrees,Point posedOrigin,float posedDegrees,float scale=1f)
            {
                double radians=(posedDegrees-bindDegrees)*Math.PI/180.0;
                cosine=(float)Math.Cos(radians)*scale;sine=(float)Math.Sin(radians)*scale;
                translation=posedOrigin-Rotate(bindOrigin,cosine,sine);
            }
            public Point Apply(Point vertex) => Rotate(vertex,cosine,sine)+translation;
        }
        public readonly struct Weights
        {
            public readonly float Root,Joint,Tip;
            public Weights(float root,float joint,float tip)
            {
                float sum=root+joint+tip;
                if(root<0||joint<0||tip<0||sum<.0001f)throw new ArgumentOutOfRangeException(nameof(root));
                Root=root/sum;Joint=joint/sum;Tip=tip/sum;
            }
        }
        public static Chain Solve(Point root,Point desired,float upper,float lower,int bendDirection)
        {
            if(upper<=0||lower<=0)throw new ArgumentOutOfRangeException(nameof(upper));
            Point delta=desired-root;
            float length=delta.Length;
            Point direction=length>.00001f?delta*(1f/length):new Point(0,-1);
            float distance=Clamp(length,Math.Abs(upper-lower)+.0001f,upper+lower-.0001f);
            Point tip=root+direction*distance;
            double bearing=Math.Atan2(direction.Y,direction.X);
            double offset=Math.Acos(Clamp((upper*upper+distance*distance-lower*lower)/(2f*upper*distance),-1,1));
            double angle=bearing+(bendDirection<0?-offset:offset);
            Point joint=root+new Point((float)Math.Cos(angle)*upper,(float)Math.Sin(angle)*upper);
            return new Chain(root,joint,tip,(float)(angle*180/Math.PI),
                (float)(Math.Atan2(tip.Y-joint.Y,tip.X-joint.X)*180/Math.PI));
        }
        public static Weights BindWeights(float down,float firstJoint,float secondJoint,float blend=.045f)
        {
            float afterFirst=Smooth(firstJoint-blend,firstJoint+blend,down);
            float afterSecond=Smooth(secondJoint-blend,secondJoint+blend,down);
            return new Weights(1-afterFirst,afterFirst*(1-afterSecond),afterSecond);
        }
        public static Point Skin(Point vertex,Weights weights,SkinTransform root,SkinTransform joint,SkinTransform tip)
            =>root.Apply(vertex)*weights.Root+joint.Apply(vertex)*weights.Joint+tip.Apply(vertex)*weights.Tip;
        public static Point Foot(float cycle,float span,float stance,float ankle,float lift,out float pitch)
        {
            cycle=cycle-(float)Math.Floor(cycle);
            if(cycle<stance)
            {
                float t=cycle/stance;
                pitch=7f*(1-Clamp(t/.16f,0,1))-10f*Smooth(.8f,1,t);
                return new Point(span*(1-2*t),ankle);
            }
            float swing=(cycle-stance)/(1-stance),travel=Smooth(0,1,swing);
            pitch=-10+17*travel;
            return new Point(-span+2*span*travel,ankle+(float)Math.Sin(swing*Math.PI)*lift);
        }
        public static Point Rotate(Point point,float cosine,float sine)
            =>new Point(point.X*cosine-point.Y*sine,point.X*sine+point.Y*cosine);
        static float Clamp(float value,float min,float max)=>Math.Max(min,Math.Min(max,value));
        static float Smooth(float min,float max,float value)
        {float t=Clamp((value-min)/(max-min),0,1);return t*t*(3-2*t);}
    }
}
