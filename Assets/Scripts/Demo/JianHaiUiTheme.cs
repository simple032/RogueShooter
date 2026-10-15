using UnityEngine;
namespace RogueShooter.Demo
{
    public static class JianHaiUiTheme
    {
        public static readonly Color Bone = new Color(.94f,.89f,.78f);
        public static readonly Color Copper = new Color(.76f,.61f,.36f);
        public static float Scale => Mathf.Clamp(Mathf.Min(Screen.width/1280f,Screen.height/720f),.65f,2f);
        public static void ShadowLabel(Rect rect,string value,GUIStyle style)
        {
            Color old=GUI.color;
            GUI.color=new Color(0f,0f,0f,.85f);
            GUI.Label(new Rect(rect.x+1.5f,rect.y+1.5f,rect.width,rect.height),value,style);
            GUI.color=old;GUI.Label(rect,value,style);
        }
        static GUIStyle _button;
        static Texture2D ButtonTexture(Color fill,Color edge)
        {
            var t=new Texture2D(8,8,TextureFormat.RGBA32,false){hideFlags=HideFlags.HideAndDontSave,filterMode=FilterMode.Point};
            var pixels=new Color[64];for(int y=0;y<8;y++)for(int x=0;x<8;x++)pixels[y*8+x]=x==0||y==0||x==7||y==7?edge:fill;
            t.SetPixels(pixels);t.Apply(false,true);return t;
        }
        public static GUIStyle MenuButton(int size=17)
        {
            if(_button==null){
                _button=new GUIStyle(GUI.skin.button){alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold,border=new RectOffset(1,1,1,1)};
                _button.normal.background=ButtonTexture(new Color(.055f,.075f,.095f),new Color(.28f,.33f,.36f));
                _button.hover.background=ButtonTexture(new Color(.16f,.13f,.09f),Copper);
                _button.active.background=ButtonTexture(new Color(.25f,.19f,.10f),Bone);
                _button.focused.background=_button.hover.background;
                _button.normal.textColor=_button.hover.textColor=_button.active.textColor=_button.focused.textColor=Bone;
            }
            _button.fontSize=size;return _button;
        }
    }
}
