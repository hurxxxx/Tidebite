using System;
using Tidebite.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Tidebite
{
    public sealed class GameUI : MonoBehaviour
    {
        private GameApp app;
        private Camera worldCamera;
        private OceanView ocean;
        private Font font;
        private RectTransform safe,menuHero;
        private GameObject menu,hud,death,pause,codex;
        private Text size,score,population,status,toast,biteText,dashText,deathText,respawnText,pauseText;
        private Text[] fishLabels;
        private RectTransform[] mapDots;
        private Image biteFill,dashFill,transition,growthFill;
        private Text growthText;
        private RawImage portrait;
        private readonly CartoonPortrait[] evolutionPreviews=new CartoonPortrait[6];
        private RectTransform labelsRoot;
        private Image[] mapImages;
        private readonly Rect[] occupiedLabels=new Rect[32];
        private int cachedMass=-1,cachedScore=-1,cachedKills=-1,cachedForm=-1,cachedLevel=-1;
        private Button respawn;
        private InputField address;
        private float toastTime,labelTimer;
        private Rect lastSafe;
        private Sprite round,circle;
        private readonly Color ink=new Color(.025f,.075f,.115f,.96f);
        private readonly Color cream=new Color(.98f,.96f,.89f);
        private readonly Color mint=new Color(.97f,.66f,.26f);
        private readonly Color muted=new Color(.65f,.80f,.82f);

        public void Build(GameApp owner,TouchControls controls,OceanView view)
        {
            app=owner;ocean=view;worldCamera=view.Camera;font=Resources.Load<Font>("NotoSansKR-Regular");
            round=MakeShape(false);circle=MakeShape(true);
            var canvas=new GameObject("Tidebite UI",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            canvas.transform.SetParent(transform,false);canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().pixelPerfect=true;
            var scaler=canvas.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,810);scaler.matchWidthOrHeight=.5f;
            safe=Rect("Safe area",canvas.transform);Stretch(safe);ApplySafeArea();
            if(FindAnyObjectByType<EventSystem>()==null)new GameObject("Event system",typeof(EventSystem),typeof(InputSystemUIInputModule));
            menu=Panel("Home",safe,new Color(0,0,0,0));Stretch((RectTransform)menu.transform);
            var menuArt=Rect("Animated menu art",menu.transform);Stretch(menuArt);menuArt.gameObject.AddComponent<Canvas>();
            PaintedImage("Menu reef",menuArt,"profile-reef",new Vector2(-35,30),new Vector2(430,430),new Vector2(1,0),0);
            PaintedImage("Menu small fish",menuArt,FishEvolution.Assets[0],new Vector2(-50,-120),new Vector2(200,200),Vector2.one,8);
            PaintedImage("Menu abyssal fish",menuArt,FishEvolution.Assets[5],new Vector2(-555,-420),new Vector2(260,260),Vector2.one,-5);
            menuHero=PaintedImage("Menu shark",menuArt,FishEvolution.Assets[4],new Vector2(-210,-175),new Vector2(460,460),Vector2.one,-6);
            Text("Logo",menu.transform,"T I D E B I T E",38,cream,new Vector2(64,-50),new Vector2(500,65),new Vector2(0,1));
            Text("Edition",menu.transform,"A LITTLE FISH. A BIG OCEAN.",14,mint,new Vector2(67,-117),new Vector2(510,28),new Vector2(0,1));
            var card=Panel("Welcome",menu.transform,ink);Place((RectTransform)card.transform,new Vector2(64,-187),new Vector2(500,474),new Vector2(0,1));
            Text("Title",card.transform,"작게 시작해서,\n바다를 삼켜요.",35,cream,new Vector2(32,-25),new Vector2(435,110),new Vector2(0,1));
            Text("Intro",card.transform,"먹고 자라며 30레벨, 6가지 모습으로 진화.\n작은 물고기를 삼키고 바다의 지배자가 되세요.",16,muted,new Vector2(34,-145),new Vector2(435,62),new Vector2(0,1));
            Button("Solo",card.transform,"혼자 바다로   →",new Vector2(32,-225),new Vector2(436,62),mint,ink,app.Solo);
            Text("Solo note",card.transform,"인터넷 없이 플레이 · 똑똑한 봇 15마리",13,muted,new Vector2(34,-296),new Vector2(432,25),new Vector2(0,1));
            address=Input(card.transform,new Vector2(32,-340),new Vector2(270,47));address.text=PlayerPrefs.GetString("server","127.0.0.1:7777");
            Button("Online",card.transform,"멀티 접속",new Vector2(312,-340),new Vector2(156,47),new Color(.2f,.39f,.43f),cream,()=>{PlayerPrefs.SetString("server",address.text);app.Connect(address.text);});
            Text("Network note",card.transform,"자체 서버 주소 · 같은 Wi-Fi에서는 서버의 LAN IP",12,muted,new Vector2(34,-402),new Vector2(432,29),new Vector2(0,1));
            Button("Growth codex",menu.transform,"성장 도감   ·   30 LV / 6 FORMS",new Vector2(64,-669),new Vector2(500,42),new Color(.04f,.17f,.23f,.94f),cream,()=>codex.SetActive(true));
            Text("Footer",menu.transform,"MOVE  WASD / 방향키        BITE  SPACE        DASH  SHIFT",11,cream,new Vector2(66,6),new Vector2(800,22),Vector2.zero);
            Text("Tag",menu.transform,"THE REEF HAS NO MERCY",13,mint,new Vector2(-36,-36),new Vector2(350,30),Vector2.one,TextAnchor.MiddleRight);

            hud=Panel("HUD",safe,Color.clear);Stretch((RectTransform)hud.transform);
            var stats=Panel("Stats",hud.transform,ink);Place((RectTransform)stats.transform,new Vector2(28,-25),new Vector2(288,140),new Vector2(0,1));
            Text("Mass label",stats.transform,"MY FISH",12,mint,new Vector2(20,-11),new Vector2(200,23),new Vector2(0,1));
            portrait=PaintedImage("My fish portrait",stats.transform,FishEvolution.Assets[0],new Vector2(-13,-12),new Vector2(76,76),Vector2.one,0).GetComponent<RawImage>();
            size=Text("Mass",stats.transform,"LV. 01",30,cream,new Vector2(18,-32),new Vector2(180,45),new Vector2(0,1));
            growthText=Text("Growth progress",stats.transform,"",12,muted,new Vector2(18,-81),new Vector2(252,28),new Vector2(0,1));
            var bar=Panel("Growth track",stats.transform,new Color(.13f,.27f,.31f));Place((RectTransform)bar.transform,new Vector2(18,-117),new Vector2(252,6),new Vector2(0,1));
            var fill=Panel("Growth fill",bar.transform,mint);Stretch((RectTransform)fill.transform);growthFill=fill.GetComponent<Image>();growthFill.type=Image.Type.Filled;growthFill.fillMethod=Image.FillMethod.Horizontal;growthFill.raycastTarget=false;
            score=Text("Score",hud.transform,"",17,cream,new Vector2(0,-27),new Vector2(500,36),new Vector2(.5f,1),TextAnchor.MiddleCenter);
            population=Text("Population",hud.transform,"",13,mint,new Vector2(0,-65),new Vector2(600,30),new Vector2(.5f,1),TextAnchor.MiddleCenter);
            status=Text("Status",hud.transform,"",14,cream,new Vector2(0,-106),new Vector2(850,48),new Vector2(.5f,1),TextAnchor.MiddleCenter);
            Button("Pause",hud.transform,"Ⅱ",new Vector2(-28,-25),new Vector2(54,48),ink,cream,app.TogglePause,new Vector2(1,1));
            var map=Panel("Minimap",hud.transform,ink);Place((RectTransform)map.transform,new Vector2(-28,-91),new Vector2(142,142),Vector2.one);
            mapDots=new RectTransform[32];mapImages=new Image[32];
            for(int i=0;i<32;i++){var dot=Panel("Dot",map.transform,muted,circle);mapDots[i]=(RectTransform)dot.transform;mapImages[i]=dot.GetComponent<Image>();Place(mapDots[i],Vector2.zero,new Vector2(4,4),Vector2.one*.5f);}
            var stickPanel=Panel("Move stick",hud.transform,new Color(.8f,1,1,.13f),circle);
            var stickRect=(RectTransform)stickPanel.transform;Place(stickRect,new Vector2(42,38),new Vector2(184,184),Vector2.zero);
            var knob=Panel("Knob",stickRect,new Color(.8f,1,1,.45f),circle);Place((RectTransform)knob.transform,Vector2.zero,new Vector2(66,66),Vector2.one*.5f);
            knob.GetComponent<Image>().raycastTarget=false;
            var stick=stickPanel.AddComponent<VirtualStick>();stick.Knob=(RectTransform)knob.transform;controls.Bind(stick);
            Text("Move label",stickRect,"이동",13,cream,new Vector2(0,-14),new Vector2(100,22),new Vector2(.5f,0),TextAnchor.MiddleCenter);
            var bite=Panel("Bite",hud.transform,new Color(1,.78f,.38f,.95f),circle);Place((RectTransform)bite.transform,new Vector2(-42,40),new Vector2(136,136),new Vector2(1,0));
            var biteAction=bite.AddComponent<PressAction>();biteAction.Press=controls.Bite;controls.BindBite(biteAction);biteFill=Cooldown(bite.transform);
            biteText=Text("Bite label",bite.transform,"한 입\nSPACE",22,ink,Vector2.zero,new Vector2(120,80),Vector2.one*.5f,TextAnchor.MiddleCenter);
            var dash=Panel("Dash",hud.transform,new Color(.53f,.93f,.84f,.9f),circle);Place((RectTransform)dash.transform,new Vector2(-202,58),new Vector2(98,98),new Vector2(1,0));
            dash.AddComponent<PressAction>().Press=controls.Dash;dashFill=Cooldown(dash.transform);
            dashText=Text("Dash label",dash.transform,"돌진\nSHIFT",16,ink,Vector2.zero,new Vector2(90,64),Vector2.one*.5f,TextAnchor.MiddleCenter);
            Text("Rule",hud.transform,"금색 진주로 성장!   ·   작은 물고기 옆에서 한 입 버튼을 꾹 누르세요",13,cream,new Vector2(0,24),new Vector2(720,30),new Vector2(.5f,0),TextAnchor.MiddleCenter);
            fishLabels=new Text[32];
            labelsRoot=Rect("Fish labels",hud.transform);Stretch(labelsRoot);labelsRoot.gameObject.AddComponent<Canvas>();
            for(int i=0;i<32;i++)fishLabels[i]=Text("Fish tag "+i,labelsRoot,"",12,cream,Vector2.zero,new Vector2(142,27),Vector2.one*.5f,TextAnchor.MiddleCenter);
            death=Modal("Death",new Vector2(440,300));
            Text("Death title",death.transform,"다음 파도를 타요",29,cream,new Vector2(28,-26),new Vector2(384,50),new Vector2(0,1),TextAnchor.MiddleCenter);
            deathText=Text("Death summary",death.transform,"",17,muted,new Vector2(25,-91),new Vector2(390,60),new Vector2(0,1),TextAnchor.MiddleCenter);
            respawn=Button("Respawn",death.transform,"다시 헤엄치기",new Vector2(36,-172),new Vector2(368,56),mint,ink,app.Respawn);
            respawnText=respawn.GetComponentInChildren<Text>();
            Button("Home",death.transform,"홈으로",new Vector2(145,-240),new Vector2(150,36),Color.clear,muted,app.Home);
            pause=Modal("Pause",new Vector2(440,330));
            pauseText=Text("Pause title",pause.transform,"잠깐 쉬어가기",27,cream,new Vector2(30,-25),new Vector2(380,60),new Vector2(0,1),TextAnchor.MiddleCenter);
            Button("Resume",pause.transform,"계속 헤엄치기",new Vector2(36,-105),new Vector2(368,56),mint,ink,app.TogglePause);
            Button("Sound",pause.transform,"소리 켜기 / 끄기",new Vector2(36,-177),new Vector2(368,48),new Color(.18f,.34f,.38f),cream,app.ToggleSound);
            Button("Quit",pause.transform,"홈으로",new Vector2(36,-243),new Vector2(368,48),Color.clear,muted,app.Home);
            BuildCodex();
            toast=Text("Toast",safe,"",20,cream,new Vector2(0,168),new Vector2(950,54),new Vector2(.5f,0),TextAnchor.MiddleCenter);
            death.SetActive(false);pause.SetActive(false);
            var fade=Panel("Scene transition",safe,Color.clear);Stretch((RectTransform)fade.transform);transition=fade.GetComponent<Image>();
        }
        public void SetMode(bool isMenu){menu.SetActive(isMenu);hud.SetActive(!isMenu);death.SetActive(false);pause.SetActive(false);codex.SetActive(false);FishPreviewAtlas.Ensure().Show(isMenu);cachedMass=cachedScore=cachedKills=cachedLevel=cachedForm=-1;labelTimer=1;}
        public void SetTransition(float opacity){transition.color=new Color(.025f,.14f,.18f,Mathf.SmoothStep(0,1,opacity));transition.raycastTarget=opacity>0;}
        public void ShowPause(bool show,bool online){pause.SetActive(show);pauseText.text=online?"온라인 바다는 계속 흘러가요":"잠깐 쉬어가기";}
        public void Notify(string message){toast.text=message;toastTime=3;}
        public void React(WorldEvent e){if(e.Kind==WorldEventKind.Eat&&e.Actor==app.Session.LocalId)Notify("한 입 성공!  +"+e.Value.ToString("0.00")+" 성장");}
        public void Refresh(IGameSession session,float dt)
        {
            ApplySafeArea();toastTime-=dt;if(toastTime<=0)toast.text="";
            if(app.Menu)
            {
                return;
            }
            int local=session.LocalId;bool has=local>=0&&local<session.Fish.Length;
            if(!has||!session.Ready){status.text=session.Status+"\n홈으로 돌아가려면 우측 Ⅱ 버튼";return;}
            var me=session.Fish[local];
            var settings=session.Settings;
            int mass=Mathf.RoundToInt(me.Mass*100);
            int level=GrowthCurve.Level(me.Mass,settings),form=GrowthCurve.Form(me.Mass,settings);
            if(cachedMass!=mass||cachedLevel!=level)
            {
                if(cachedLevel>0&&level>cachedLevel&&me.Alive)Notify(form!=cachedForm?"진화!  "+FishEvolution.Names[form]+"  ·  LV. "+level:"LEVEL UP  ·  LV. "+level);
                cachedMass=mass;cachedLevel=level;size.text="LV. "+level.ToString("00");
                growthText.text=FishEvolution.Names[form]+"  ·  "+me.Mass.ToString("0.00")+"×";
            }
            if(cachedForm!=form){cachedForm=form;portrait.texture=FishEvolution.Texture(form);}
            growthFill.fillAmount=Mathf.Lerp(growthFill.fillAmount,GrowthCurve.Progress(me.Mass,settings),1-Mathf.Exp(-dt*12));
            if(cachedScore!=me.Score||cachedKills!=me.Kills){cachedScore=me.Score;cachedKills=me.Kills;score.text=$"{me.Score:N0} 점     ·     {me.Kills} 마리 포식";}
            labelTimer+=dt;bool updateText=labelTimer>=.10f;if(updateText)labelTimer=0;
            if(updateText)
            {
                int bots=0,humans=0;foreach(var f in session.Fish)if(f.Occupied){if(f.IsBot)bots++;else humans++;}
                population.text=session.IsOnline?$"ONLINE  ·  시야 내 플레이어 {humans} / 봇 {bots}":$"SOLO  ·  나 + 봇 {bots}";
            }
            status.text=me.Protection>0?"보호 중 · 공격하면 보호가 해제됩니다":"";
            biteFill.fillAmount=Mathf.Clamp01(me.BiteCooldown/settings.BiteCooldown);dashFill.fillAmount=Mathf.Clamp01(me.DashCooldown/settings.DashCooldown);
            if(updateText){biteText.text=me.BiteCooldown>.05f?me.BiteCooldown.ToString("0.0"):"한 입\nSPACE";dashText.text=me.DashCooldown>.05f?me.DashCooldown.ToString("0.0"):"돌진\nSHIFT";}
            death.SetActive(!me.Alive&&!app.Paused);
            if(!me.Alive){deathText.text=$"{me.Score:N0} 점  ·  {me.Kills} 마리 포식\n이번 바다에서 한 뼘 더 자랐어요.";respawn.interactable=me.RespawnRemaining<=0;respawnText.text=me.RespawnRemaining>0?$"{Mathf.CeilToInt(me.RespawnRemaining)}초 후 다시 도전":"다시 헤엄치기";}
            int shown=0;float uiScale=Screen.height/810f;
            for(int pass=0;pass<session.Fish.Length;pass++)
            {
                int i=pass==0?local:pass<=local?pass-1:pass;
                var f=session.Fish[i];bool active=f.Occupied&&f.Alive;
                mapDots[i].gameObject.SetActive(active);
                if(active){mapDots[i].anchoredPosition=new Vector2(f.Position.X,f.Position.Y)/settings.WorldHalfSize*62;mapImages[i].color=i==local?cream:f.Mass>=me.Mass*settings.ConsumeRatio?new Color(1,.48f,.43f):mint;}
                var screen=worldCamera.WorldToScreenPoint(ocean.LabelPosition(i));screen.x=Mathf.Round(screen.x);screen.y=Mathf.Round(screen.y);
                bool visible=active&&me.Alive&&!app.Paused&&screen.z>0&&screen.x>0&&screen.x<Screen.width&&screen.y>100*uiScale&&screen.y<Screen.height-100*uiScale;
                var bounds=new Rect(screen.x-71*uiScale,screen.y-14*uiScale,142*uiScale,28*uiScale);
                for(int k=0;k<shown&&visible;k++)if(occupiedLabels[k].Overlaps(bounds))visible=false;
                fishLabels[i].gameObject.SetActive(visible);
                if(!visible)continue;
                occupiedLabels[shown++]=bounds;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelsRoot,screen,null,out var position);fishLabels[i].rectTransform.anchoredPosition=position;
                string tag=i==local?"나":f.Protection>0?"보호":f.Mass>=me.Mass*settings.ConsumeRatio?"! 위험":PredationRules.CanConsume(me,f,settings)?"↓ 한 입":"비슷한 크기";
                if(updateText||fishLabels[i].text.Length==0)fishLabels[i].text=tag+"  LV."+GrowthCurve.Level(f.Mass,settings).ToString("00")+" · "+f.Mass.ToString("0.0")+"×";
                fishLabels[i].color=tag=="! 위험"?new Color(1,.66f,.55f):tag=="↓ 한 입"?mint:cream;
            }
        }
        private void ApplySafeArea()
        {var r=Screen.safeArea;if(r==lastSafe)return;lastSafe=r;safe.anchorMin=new Vector2(r.x/Screen.width,r.y/Screen.height);safe.anchorMax=new Vector2(r.xMax/Screen.width,r.yMax/Screen.height);safe.offsetMin=safe.offsetMax=Vector2.zero;}
        private RectTransform PaintedImage(string name,Transform parent,string asset,Vector2 position,Vector2 dimensions,Vector2 anchor,float angle)
        {
            var rect=Rect(name,parent);Place(rect,position,dimensions,anchor);
            rect.pivot=Vector2.one*.5f;rect.anchoredPosition+=new Vector2((.5f-anchor.x)*dimensions.x,(.5f-anchor.y)*dimensions.y);
            rect.localRotation=Quaternion.Euler(0,0,angle);
            int form=Array.IndexOf(FishEvolution.Assets,asset);
            RawImage image=(name.StartsWith("Menu")||name.StartsWith("Evolution"))&&form>=0?rect.gameObject.AddComponent<CartoonPortrait>():rect.gameObject.AddComponent<RawImage>();
            if(image is CartoonPortrait animated)animated.Configure(form);else image.texture=Resources.Load<Texture2D>("Art/"+asset);
            image.raycastTarget=false;
            return rect;
        }
        private void BuildCodex()
        {
            codex=Modal("Growth codex",new Vector2(1000,570));
            codex.GetComponent<Image>().color=new Color(.025f,.075f,.115f,1);
            Text("Codex title",codex.transform,"작은 물고기에서, 심해의 지배자로",27,cream,new Vector2(30,-22),new Vector2(900,50),new Vector2(0,1));
            Text("Codex intro",codex.transform,"먹는 즉시 크기가 자랍니다. 30개 레벨을 거쳐 5레벨마다 턱·몸통·지느러미가 달라집니다.",15,muted,new Vector2(32,-82),new Vector2(936,45),new Vector2(0,1));
            for(int i=0;i<6;i++)
            {
                var card=Panel("Form "+i,codex.transform,new Color(.055f,.18f,.23f,.98f));
                Place((RectTransform)card.transform,new Vector2(30+i*158,-153),new Vector2(150,282),new Vector2(0,1));
                Text("Range",card.transform,$"LV. {i*5+1:00} — {i*5+5:00}",13,mint,new Vector2(8,-9),new Vector2(134,27),new Vector2(0,1),TextAnchor.MiddleCenter);
                evolutionPreviews[i]=PaintedImage("Evolution "+i,card.transform,FishEvolution.Assets[i],new Vector2(0,-45),new Vector2(148,168),new Vector2(.5f,1),0).GetComponent<CartoonPortrait>();
                Text("Form name",card.transform,FishEvolution.Names[i],14,cream,new Vector2(6,-220),new Vector2(138,27),new Vector2(0,1),TextAnchor.MiddleCenter);
                float mass=GrowthCurve.Threshold(i*5+1,app.Config);
                Text("Form scale",card.transform,$"질량 {mass:0.00}× 부터",11,muted,new Vector2(6,-251),new Vector2(138,22),new Vector2(0,1),TextAnchor.MiddleCenter);
            }
            Button("Preview bite",codex.transform,"공격 동작 느리게 보기",new Vector2(240,-476),new Vector2(250,54),mint,ink,()=>{foreach(var preview in evolutionPreviews)preview.Attack();});
            Button("Close codex",codex.transform,"닫기",new Vector2(510,-476),new Vector2(250,54),new Color(.15f,.30f,.35f),cream,()=>codex.SetActive(false));
            codex.SetActive(false);
        }
        private Image Cooldown(Transform parent)
        {var p=Panel("Cooldown",parent,new Color(.03f,.13f,.17f,.55f),circle);Stretch((RectTransform)p.transform);var image=p.GetComponent<Image>();image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Radial360;image.fillAmount=0;image.raycastTarget=false;return image;}
        private GameObject Modal(string name,Vector2 dimensions)
        {var go=Panel(name,safe,ink);Place((RectTransform)go.transform,Vector2.zero,dimensions,Vector2.one*.5f);return go;}
        private InputField Input(Transform parent,Vector2 position,Vector2 dimensions)
        {
            var go=Panel("Server address",parent,new Color(.07f,.23f,.28f));Place((RectTransform)go.transform,position,dimensions,new Vector2(0,1));
            var input=go.AddComponent<InputField>();var label=Text("Value",go.transform,"",16,cream,Vector2.zero,dimensions-new Vector2(24,0),Vector2.one*.5f);
            input.textComponent=label;input.characterLimit=128;input.contentType=InputField.ContentType.Standard;input.lineType=InputField.LineType.SingleLine;return input;
        }
        private Button Button(string name,Transform parent,string label,Vector2 position,Vector2 dimensions,Color fill,Color textColor,Action click,Vector2? anchor=null)
        {var go=Panel(name,parent,fill);Place((RectTransform)go.transform,position,dimensions,anchor??new Vector2(0,1));var button=go.AddComponent<Button>();button.targetGraphic=go.GetComponent<Image>();button.targetGraphic.raycastTarget=true;button.onClick.AddListener(()=>click());Text("Label",go.transform,label,19,textColor,Vector2.zero,dimensions,Vector2.one*.5f,TextAnchor.MiddleCenter);return button;}
        private Text Text(string name,Transform parent,string value,int fontSize,Color color,Vector2 position,Vector2 dimensions,Vector2 anchor,TextAnchor align=TextAnchor.MiddleLeft)
        {var rect=Rect(name,parent);Place(rect,position,dimensions,anchor);var label=rect.gameObject.AddComponent<Text>();label.font=font;label.text=value;label.fontSize=fontSize;label.color=color;label.alignment=align;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;}
        private GameObject Panel(string name,Transform parent,Color color,Sprite sprite=null)
        {var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite??round;image.type=Image.Type.Sliced;image.color=color;image.raycastTarget=color.a>0;return rect.gameObject;}
        private static RectTransform Rect(string name,Transform parent){var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        private static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void Place(RectTransform r,Vector2 position,Vector2 dimensions,Vector2 anchor){r.anchorMin=r.anchorMax=r.pivot=anchor;r.sizeDelta=dimensions;r.anchoredPosition=position;}
        private static Sprite MakeShape(bool circleShape)
        {
            const int n=64;var texture=new Texture2D(n,n,TextureFormat.RGBA32,false);texture.wrapMode=TextureWrapMode.Clamp;texture.filterMode=FilterMode.Bilinear;
            var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {float dx=Mathf.Abs(x-31.5f),dy=Mathf.Abs(y-31.5f);float distance=circleShape?Mathf.Sqrt(dx*dx+dy*dy)-30:Mathf.Sqrt(Mathf.Pow(Mathf.Max(dx-20,0),2)+Mathf.Pow(Mathf.Max(dy-20,0),2))-11;pixels[y*n+x]=new Color(1,1,1,Mathf.Clamp01(.5f-distance));}
            texture.SetPixels(pixels);texture.Apply(false,true);return Sprite.Create(texture,new Rect(0,0,n,n),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,circleShape?Vector4.zero:new Vector4(16,16,16,16));
        }
    }
}
