using UnityEngine.UI;

namespace Tidebite
{
    public sealed class CartoonPortrait : RawImage
    {
        private int form;
        public void Configure(int value)
        {
            form=value;var atlas=FishPreviewAtlas.Ensure();texture=atlas.Texture;uvRect=atlas.UV(form);raycastTarget=false;
        }
        public void Attack(){FishPreviewAtlas.Ensure().Attack(form);}
    }
}
