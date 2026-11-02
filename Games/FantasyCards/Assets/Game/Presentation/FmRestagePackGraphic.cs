using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>New remaster packaging. Offer identity never depends on purchased contents or RNG.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmRestagePackGraphic : MaskableGraphic
    {
        public string OfferId, StyleKey;
        public int ArtworkTile;
        public bool IsOpeningBack, IsDeck;
        public Color Accent = new Color(.79f, .68f, .40f);
        protected FmRestagePackGraphic() { useLegacyMeshGeneration = false; }
        protected override void Awake() { base.Awake(); raycastTarget = false; }
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear(); var r = GetPixelAdjustedRect();
            Vector2 P(float x, float y) => new Vector2(Mathf.Lerp(r.xMin,r.xMax,x),Mathf.Lerp(r.yMin,r.yMax,y));
            void Quad(float x,float y,float xx,float yy,Color tint)
            { int n=mesh.currentVertCount;mesh.AddVert(P(x,y),tint,Vector2.zero);mesh.AddVert(P(x,yy),tint,Vector2.zero);mesh.AddVert(P(xx,yy),tint,Vector2.zero);mesh.AddVert(P(xx,y),tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3); }
            void Stroke(float x,float y,float xx,float yy,float width,Color tint)
            { Vector2 a=P(x,y),b=P(xx,yy),normal=new Vector2(-(b-a).y,(b-a).x).normalized*width*.5f;int n=mesh.currentVertCount;mesh.AddVert(a-normal,tint,Vector2.zero);mesh.AddVert(a+normal,tint,Vector2.zero);mesh.AddVert(b+normal,tint,Vector2.zero);mesh.AddVert(b-normal,tint,Vector2.zero);mesh.AddTriangle(n,n+1,n+2);mesh.AddTriangle(n,n+2,n+3); }
            float pixel=Mathf.Max(.65f,Mathf.Min(r.width,r.height)/180);
            Quad(0,0,1,1,new Color(.011f,.022f,.039f,1));
            Quad(.035f,.02f,.965f,.98f,Color.Lerp(new Color(.025f,.04f,.075f),Accent,.12f));
            for(int i=0;i<18;i++)
            {
                float y=.03f+i*.053f;var tint=new Color(Accent.r,Accent.g,Accent.b,i%3==0?.13f:.045f);
                Stroke(.05f,y,.95f,Mathf.Min(.97f,y+.12f),pixel,tint);
            }
            for(int b=0;b<3;b++)
            {
                float d=.022f+b*.022f;var tint=new Color(Accent.r,Accent.g,Accent.b,b==1?.30f:.92f);
                Stroke(d,d,1-d,d,pixel,tint);Stroke(1-d,d,1-d,1-d,pixel,tint);
                Stroke(1-d,1-d,d,1-d,pixel,tint);Stroke(d,1-d,d,d,pixel,tint);
            }
            void Ring(float cx,float cy,float rx,float ry,Color tint,float turn=0)
            {
                float ox=cx+Mathf.Cos(turn)*rx,oy=cy+Mathf.Sin(turn)*ry;
                for(int i=1;i<=64;i++){float a=turn+i*Mathf.PI/32;float x=cx+Mathf.Cos(a)*rx,y=cy+Mathf.Sin(a)*ry;Stroke(ox,oy,x,y,pixel,tint);ox=x;oy=y;}
            }
            var glow=new Color(Accent.r,Accent.g,Accent.b,.20f);
            Ring(.5f,.555f,.322f,.212f,glow);Ring(.5f,.555f,.285f,.188f,Accent);
            Ring(.5f,.555f,.265f,.175f,new Color(Accent.r,Accent.g,Accent.b,.42f));
            if(ArtworkTile==1)
            {
                // Faceted golden crystal marks the guaranteed premium product.
                float[] xs={.23f,.36f,.64f,.77f,.5f,.23f},ys={.63f,.735f,.735f,.63f,.365f,.63f};
                for(int i=0;i<5;i++)Stroke(xs[i],ys[i],xs[i+1],ys[i+1],pixel*1.6f,Accent);
                Stroke(.23f,.63f,.77f,.63f,pixel,Accent);Stroke(.36f,.735f,.5f,.365f,pixel,Accent);Stroke(.64f,.735f,.5f,.365f,pixel,Accent);
                for(int i=0;i<8;i++){float a=i*Mathf.PI/4;Stroke(.5f+Mathf.Cos(a)*.31f,.555f+Mathf.Sin(a)*.205f,.5f+Mathf.Cos(a)*.355f,.555f+Mathf.Sin(a)*.235f,pixel,Accent);}
            }
            else if(ArtworkTile==2)
            {
                Stroke(.32f,.41f,.70f,.70f,pixel*3,Accent);Stroke(.30f,.46f,.39f,.39f,pixel*3,Accent);
                Stroke(.68f,.41f,.30f,.70f,pixel*3,Accent);Stroke(.61f,.39f,.70f,.46f,pixel*3,Accent);
            }
            else if(ArtworkTile==3)
            {
                Stroke(.5f,.40f,.5f,.70f,pixel*1.6f,Accent);
                Stroke(.23f,.44f,.23f,.70f,pixel*1.6f,Accent);Stroke(.77f,.44f,.77f,.70f,pixel*1.6f,Accent);
                Stroke(.23f,.70f,.5f,.66f,pixel*1.6f,Accent);Stroke(.77f,.70f,.5f,.66f,pixel*1.6f,Accent);
                Stroke(.23f,.44f,.5f,.40f,pixel*1.6f,Accent);Stroke(.77f,.44f,.5f,.40f,pixel*1.6f,Accent);
                for(int i=0;i<3;i++){float y=.5f+i*.045f;Stroke(.29f,y,.44f,y-.02f,pixel,Accent);Stroke(.56f,y-.02f,.71f,y,pixel,Accent);}
            }
            else if(ArtworkTile==4)
            {
                Stroke(.5f,.39f,.5f,.70f,pixel*2,Accent);Stroke(.5f,.47f,.30f,.60f,pixel*2,Accent);Stroke(.5f,.56f,.70f,.68f,pixel*2,Accent);
                Ring(.33f,.61f,.10f,.045f,Accent);Ring(.67f,.68f,.10f,.045f,Accent);Ring(.5f,.715f,.055f,.07f,Accent);
            }
            else if(ArtworkTile==0)
            {
                for(int i=0;i<7;i++)
                {
                    float a=i*Mathf.PI*2/7+Mathf.PI*.5f,b=a+Mathf.PI*6/7;
                    Stroke(.5f+Mathf.Cos(a)*.225f,.555f+Mathf.Sin(a)*.148f,.5f+Mathf.Cos(b)*.225f,.555f+Mathf.Sin(b)*.148f,pixel,Accent);
                    Ring(.5f+Mathf.Cos(a)*.225f,.555f+Mathf.Sin(a)*.148f,.018f,.012f,Accent);
                }
            }
            // Folded foil edges and a distinct thick book spine for fixed decks.
            Quad(.045f,.935f,.955f,.971f,new Color(Accent.r,Accent.g,Accent.b,.65f));
            Quad(.045f,.029f,.955f,.065f,new Color(Accent.r,Accent.g,Accent.b,.5f));
            if(IsDeck)Quad(.05f,.065f,.13f,.934f,new Color(Accent.r,Accent.g,Accent.b,.34f));
            for(int i=0;i<15;i++)
            {float x=.065f+i*.062f;Stroke(x,.94f,x,.968f,pixel*.65f,new Color(.04f,.055f,.08f,.7f));Stroke(x,.034f,x,.061f,pixel*.65f,new Color(.04f,.055f,.08f,.7f));}
            for(int i=0;i<4;i++)
            {
                float x=(i&1)==0?.1f:.9f,y=(i&2)==0?.11f:.89f;
                Stroke(x-.021f,y,x+.021f,y,pixel*1.6f,Accent);Stroke(x,y-.015f,x,y+.015f,pixel*1.6f,Accent);
            }
        }
    }
}
