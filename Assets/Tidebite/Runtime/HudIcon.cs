using UnityEngine;
using UnityEngine.UI;

namespace Tidebite
{
    // Vector glyphs remain sharp on phones/tablets without another texture atlas.
    public sealed class HudIcon : MaskableGraphic
    {
        public enum Symbol { Bite, Dash, Pause, Compass, Diamond, Shield, Chevron, Ring }
        public Symbol Shape;
        public float Thickness=2;
        protected override void OnPopulateMesh(VertexHelper m)
        {
            m.Clear();float s=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.5f;Vector2 c=rectTransform.rect.center;
            if(Shape==Symbol.Ring){Arc(m,c,s*.87f,0,360,Thickness);return;}
            if(Shape==Symbol.Pause)
            {Line(m,c+new Vector2(-.25f,-.5f)*s,c+new Vector2(-.25f,.5f)*s,Thickness*1.8f);Line(m,c+new Vector2(.25f,-.5f)*s,c+new Vector2(.25f,.5f)*s,Thickness*1.8f);return;}
            if(Shape==Symbol.Bite)
            {
                Arc(m,c+new Vector2(0,.1f)*s,s*.75f,25,155,Thickness*1.4f);
                Arc(m,c-new Vector2(0,.1f)*s,s*.75f,205,335,Thickness*1.4f);
                for(int i=0;i<4;i++)
                {
                    float x=(-.48f+i*.32f)*s;
                    Triangle(m,c+new Vector2(x-.10f*s,.62f*s),c+new Vector2(x+.10f*s,.62f*s),c+new Vector2(x,.24f*s));
                    Triangle(m,c+new Vector2(x-.10f*s,-.62f*s),c+new Vector2(x,-.24f*s),c+new Vector2(x+.10f*s,-.62f*s));
                }
                return;
            }
            if(Shape==Symbol.Dash)
            {
                Triangle(m,c+new Vector2(-.12f,.82f)*s,c+new Vector2(-.55f,-.12f)*s,c+new Vector2(.28f,.04f)*s);
                Triangle(m,c+new Vector2(-.28f,-.04f)*s,c+new Vector2(.55f,.12f)*s,c+new Vector2(.12f,-.82f)*s);
                Line(m,c+new Vector2(-.8f,.35f)*s,c+new Vector2(-.45f,.35f)*s,Thickness);
                Line(m,c+new Vector2(.4f,-.35f)*s,c+new Vector2(.8f,-.35f)*s,Thickness);return;
            }
            if(Shape==Symbol.Chevron)
            {Line(m,c+new Vector2(-.5f,.22f)*s,c+new Vector2(0,-.22f)*s,Thickness);Line(m,c+new Vector2(0,-.22f)*s,c+new Vector2(.5f,.22f)*s,Thickness);return;}
            if(Shape==Symbol.Shield)
            {
                var p=new[]{new Vector2(0,.8f),new Vector2(.6f,.54f),new Vector2(.5f,-.25f),new Vector2(0,-.8f),new Vector2(-.5f,-.25f),new Vector2(-.6f,.54f)};
                for(int i=0;i<p.Length;i++)Line(m,c+p[i]*s,c+p[(i+1)%p.Length]*s,Thickness);return;
            }
            if(Shape==Symbol.Compass){Arc(m,c,s*.78f,0,360,Thickness*.65f);Triangle(m,c+new Vector2(0,.7f)*s,c+new Vector2(-.35f,-.4f)*s,c+new Vector2(.35f,-.4f)*s);return;}
            Triangle(m,c+new Vector2(0,.8f)*s,c+new Vector2(.6f,0)*s,c+new Vector2(0,-.8f)*s);
            Triangle(m,c+new Vector2(0,.8f)*s,c+new Vector2(0,-.8f)*s,c+new Vector2(-.6f,0)*s);
        }
        private void Arc(VertexHelper m,Vector2 c,float r,float from,float to,float width)
        {
            int segments=Mathf.CeilToInt((to-from)/4);Vector2 previous=c+new Vector2(Mathf.Cos(from*Mathf.Deg2Rad),Mathf.Sin(from*Mathf.Deg2Rad))*r;
            for(int i=1;i<=segments;i++){float a=Mathf.Lerp(from,to,i/(float)segments)*Mathf.Deg2Rad;var p=c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r;Line(m,previous,p,width);previous=p;}
        }
        private void Line(VertexHelper m,Vector2 a,Vector2 b,float width)
        {
            Vector2 d=(b-a).normalized;var n=new Vector2(-d.y,d.x)*width*.5f;int k=m.currentVertCount;
            m.AddVert(a-n,color,Vector2.zero);m.AddVert(a+n,color,Vector2.zero);m.AddVert(b+n,color,Vector2.zero);m.AddVert(b-n,color,Vector2.zero);
            m.AddTriangle(k,k+1,k+2);m.AddTriangle(k,k+2,k+3);
        }
        private void Triangle(VertexHelper m,Vector2 a,Vector2 b,Vector2 c)
        {int k=m.currentVertCount;m.AddVert(a,color,Vector2.zero);m.AddVert(b,color,Vector2.zero);m.AddVert(c,color,Vector2.zero);m.AddTriangle(k,k+1,k+2);}
    }
}
