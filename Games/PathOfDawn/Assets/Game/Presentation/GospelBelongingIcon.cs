using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>Original vector silhouettes remain crisp at the inventory's canvas scale.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GospelBelongingIcon : MaskableGraphic
    {
        public GospelBelongingIcon() { useLegacyMeshGeneration=false; }
        public string ItemId;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            switch(ItemId)
            {
                case "linen-tunic":
                    Polygon(vh,new[]{new Vector2(.35f,.85f),new Vector2(.18f,.77f),new Vector2(.08f,.51f),new Vector2(.26f,.45f),new Vector2(.30f,.57f),new Vector2(.27f,.14f),new Vector2(.73f,.14f),new Vector2(.70f,.57f),new Vector2(.74f,.45f),new Vector2(.92f,.51f),new Vector2(.82f,.77f),new Vector2(.65f,.85f),new Vector2(.57f,.73f),new Vector2(.43f,.73f)});
                    Line(vh,new Vector2(.32f,.42f),new Vector2(.68f,.42f),.025f);break;
                case "leather-sandals":
                    Ellipse(vh,new Vector2(.34f,.46f),new Vector2(.19f,.34f),.035f);Ellipse(vh,new Vector2(.72f,.53f),new Vector2(.16f,.31f),.035f);
                    Line(vh,new Vector2(.21f,.60f),new Vector2(.46f,.50f),.06f);Line(vh,new Vector2(.22f,.38f),new Vector2(.46f,.50f),.06f);
                    Line(vh,new Vector2(.61f,.65f),new Vector2(.82f,.55f),.05f);Line(vh,new Vector2(.61f,.43f),new Vector2(.82f,.55f),.05f);break;
                case "water-skin":
                    Polygon(vh,new[]{new Vector2(.41f,.86f),new Vector2(.60f,.86f),new Vector2(.59f,.67f),new Vector2(.78f,.52f),new Vector2(.81f,.31f),new Vector2(.65f,.13f),new Vector2(.35f,.13f),new Vector2(.19f,.31f),new Vector2(.22f,.52f),new Vector2(.41f,.67f)});
                    Line(vh,new Vector2(.36f,.76f),new Vector2(.65f,.76f),.045f);break;
                case "travel-lamp":
                    Ellipse(vh,new Vector2(.43f,.32f),new Vector2(.30f,.13f),.045f);Line(vh,new Vector2(.68f,.33f),new Vector2(.85f,.47f),.08f);
                    Polygon(vh,new[]{new Vector2(.75f,.50f),new Vector2(.71f,.63f),new Vector2(.82f,.89f),new Vector2(.90f,.65f),new Vector2(.85f,.51f)});
                    Ellipse(vh,new Vector2(.17f,.36f),new Vector2(.11f,.11f),.035f);break;
                case "reading-notes":
                    Polygon(vh,new[]{new Vector2(.17f,.17f),new Vector2(.17f,.85f),new Vector2(.76f,.85f),new Vector2(.84f,.77f),new Vector2(.84f,.17f)});
                    Line(vh,new Vector2(.30f,.18f),new Vector2(.30f,.84f),.035f);
                    Line(vh,new Vector2(.41f,.65f),new Vector2(.70f,.65f),.035f);Line(vh,new Vector2(.41f,.53f),new Vector2(.70f,.53f),.035f);Line(vh,new Vector2(.41f,.41f),new Vector2(.64f,.41f),.035f);break;
                default:
                    Polygon(vh,new[]{new Vector2(.10f,.19f),new Vector2(.10f,.80f),new Vector2(.38f,.89f),new Vector2(.65f,.79f),new Vector2(.90f,.87f),new Vector2(.90f,.25f),new Vector2(.65f,.18f),new Vector2(.38f,.28f)});
                    Line(vh,new Vector2(.38f,.28f),new Vector2(.38f,.89f),.025f);Line(vh,new Vector2(.65f,.18f),new Vector2(.65f,.79f),.025f);
                    Line(vh,new Vector2(.22f,.38f),new Vector2(.48f,.61f),.035f);Line(vh,new Vector2(.48f,.61f),new Vector2(.78f,.51f),.035f);break;
            }
        }
        Vector3 Point(Vector2 p)=>new Vector3(rectTransform.rect.xMin+p.x*rectTransform.rect.width,rectTransform.rect.yMin+p.y*rectTransform.rect.height,0);
        void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d)
        {int n=vh.currentVertCount;vh.AddVert(Point(a),color,Vector2.zero);vh.AddVert(Point(b),color,Vector2.zero);vh.AddVert(Point(c),color,Vector2.zero);vh.AddVert(Point(d),color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);}
        void Line(VertexHelper vh,Vector2 a,Vector2 b,float width)
        {var delta=(b-a).normalized;var normal=new Vector2(-delta.y,delta.x)*width/2;Quad(vh,a-normal,a+normal,b+normal,b-normal);}
        void Polygon(VertexHelper vh,Vector2[] points)
        {for(int i=0;i<points.Length;i++)Line(vh,points[i],points[(i+1)%points.Length],.035f);}
        void Ellipse(VertexHelper vh,Vector2 center,Vector2 radius,float width)
        {for(int i=0;i<32;i++){float a=i*Mathf.PI/16,b=(i+1)*Mathf.PI/16;Line(vh,center+new Vector2(Mathf.Cos(a)*radius.x,Mathf.Sin(a)*radius.y),center+new Vector2(Mathf.Cos(b)*radius.x,Mathf.Sin(b)*radius.y),width);}}
    }
}
