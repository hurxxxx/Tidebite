using UnityEngine;
using UnityEngine.Rendering;

namespace Tidebite
{
    internal static class SpriteArt
    {
        private static Mesh quad;
        public static Mesh Quad => quad != null ? quad : (quad=Grid(1,1,1,1));

        public static Material Painted(string asset)
        {
            var texture=Resources.Load<Texture2D>("Art/"+asset);
            if(texture==null) throw new System.InvalidOperationException("Missing Tidebite art: "+asset);
            var material=new Material(Shader.Find("Tidebite/PaintedSprite")){name=asset,enableInstancing=true};
            material.SetTexture("_MainTex",texture);
            return material;
        }

        public static Material Effect(Color color,float ring)
        {
            var material=new Material(Shader.Find("Tidebite/WaterFX")){enableInstancing=true};
            material.SetColor("_Tint",color); material.SetFloat("_Ring",ring);
            return material;
        }

        public static Material Action(int mode)
        {
            var material=new Material(Shader.Find("Tidebite/ActionFX")){enableInstancing=true};
            material.SetFloat("_Mode",mode);return material;
        }

        public static Transform Part(string name,Transform parent,Mesh mesh,Material material,Vector3 pos,Vector3 scale,int order)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false);
            go.transform.localPosition=pos; go.transform.localScale=scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.sortingOrder=order; renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return go.transform;
        }

        public static Mesh Grid(float width,float height,int columns,int rows)
        {
            var vertices=new Vector3[(columns+1)*(rows+1)]; var uv=new Vector2[vertices.Length];
            var indices=new int[columns*rows*6];
            for(int y=0;y<=rows;y++) for(int x=0;x<=columns;x++)
            {
                int i=y*(columns+1)+x; uv[i]=new Vector2(x/(float)columns,y/(float)rows);
                vertices[i]=new Vector3((uv[i].x-.5f)*width,0,(uv[i].y-.5f)*height);
            }
            int k=0;
            for(int y=0;y<rows;y++) for(int x=0;x<columns;x++)
            {
                int a=y*(columns+1)+x,b=a+columns+1;
                indices[k++]=a; indices[k++]=b; indices[k++]=a+1;
                indices[k++]=a+1; indices[k++]=b; indices[k++]=b+1;
            }
            var mesh=new Mesh{name="Painted sprite grid"}; mesh.vertices=vertices; mesh.uv=uv; mesh.triangles=indices;
            mesh.bounds=new Bounds(Vector3.zero,new Vector3(width+.5f,.1f,height+.3f));
            return mesh;
        }
    }
}
