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
        private Text growthText,healthText;
        private Image healthFill;
        private int cachedHealth=-1;
        private Image[] fishHealth;
        private readonly Text[] damageNumbers=new Text[16];
        private readonly float[] damageLife=new float[16];
        private readonly V2[] damagePositions=new V2[16];
        private int damageIndex;
        private RawImage portrait,nextPortrait;
        private Text nextTitle,nextProgress;
        private Image nextFill;
        private RectTransform mapViewport;
        private readonly GameObject[] healthTracks=new GameObject[GameConfig.FishCapacity];
        private readonly CartoonPortrait[] evolutionPreviews=new CartoonPortrait[12];
        private RectTransform labelsRoot;
        private Image[] mapImages;
        private readonly Rect[] occupiedLabels=new Rect[GameConfig.FishCapacity];
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
            Text("Intro",card.transform,"먹고 자라며 30레벨, 12종 물고기로 진화.\n작은 물고기를 삼키고 바다의 지배자가 되세요.",16,muted,new Vector2(34,-145),new Vector2(435,62),new Vector2(0,1));
            Button("Solo",card.transform,"혼자 바다로   →",new Vector2(32,-225),new Vector2(436,62),mint,ink,app.Solo);
            Text("Solo note",card.transform,"인터넷 없이 플레이 · 넓은 바다와 봇 47마리",13,muted,new Vector2(34,-296),new Vector2(432,25),new Vector2(0,1));
            address=Input(card.transform,new Vector2(32,-340),new Vector2(270,47));address.text=PlayerPrefs.GetString("server","127.0.0.1:7777");
            Button("Online",card.transform,"멀티 접속",new Vector2(312,-340),new Vector2(156,47),new Color(.2f,.39f,.43f),cream,()=>{PlayerPrefs.SetString("server",address.text);app.Connect(address.text);});
            Text("Network note",card.transform,"자체 서버 주소 · 같은 Wi-Fi에서는 서버의 LAN IP",12,muted,new Vector2(34,-402),new Vector2(432,29),new Vector2(0,1));
            Button("Growth codex",menu.transform,"성장 도감   ·   30 LV / 12 SPECIES",new Vector2(64,-669),new Vector2(500,42),new Color(.04f,.17f,.23f,.94f),cream,()=>codex.SetActive(true));
            Text("Footer",menu.transform,"MOVE  WASD / 방향키        BITE  SPACE        DASH  SHIFT",11,cream,new Vector2(66,6),new Vector2(800,22),Vector2.zero);
            Text("Tag",menu.transform,"THE REEF HAS NO MERCY",13,mint,new Vector2(-36,-36),new Vector2(350,30),Vector2.one,TextAnchor.MiddleRight);

            hud=Panel("HUD",safe,Color.clear);Stretch((RectTransform)hud.transform);
            var stats=Panel("Player HUD",hud.transform,new Color(.016f,.045f,.07f,.88f));
            Place((RectTransform)stats.transform,new Vector2(24,-24),new Vector2(320,112),new Vector2(0,1));
            var frame=Panel("Portrait inset",stats.transform,new Color(.15f,.29f,.32f,.5f));
            Place((RectTransform)frame.transform,new Vector2(10,-13),new Vector2(78,78),new Vector2(0,1));
            portrait=PaintedImage("My fish portrait",stats.transform,FishEvolution.Assets[0],new Vector2(6,-8),new Vector2(88,88),new Vector2(0,1),0).GetComponent<RawImage>();
            var badge=Panel("Level badge",stats.transform,mint);Place((RectTransform)badge.transform,new Vector2(22,-79),new Vector2(54,24),new Vector2(0,1));
            size=Text("Level",badge.transform,"LV. 01",12,ink,Vector2.zero,new Vector2(54,24),Vector2.one*.5f,TextAnchor.MiddleCenter);size.fontStyle=FontStyle.Bold;
            growthText=Text("Species",stats.transform,"",17,cream,new Vector2(104,-12),new Vector2(198,27),new Vector2(0,1));
            healthText=Text("Health value",stats.transform,"체력 100 / 100",10,muted,new Vector2(104,-44),new Vector2(198,17),new Vector2(0,1));
            healthFill=Track(stats.transform,"Health",new Vector2(104,-66),new Vector2(194,7),new Color(.38f,.91f,.73f));
            Text("Growth caption",stats.transform,"성장",10,mint,new Vector2(104,-80),new Vector2(190,15),new Vector2(0,1));
            growthFill=Track(stats.transform,"Growth",new Vector2(104,-99),new Vector2(194,4),mint);
            var next=Panel("Next evolution",hud.transform,new Color(.018f,.052f,.076f,.78f));
            Place((RectTransform)next.transform,new Vector2(24,-143),new Vector2(320,58),new Vector2(0,1));
            nextPortrait=PaintedImage("Next silhouette",next.transform,FishEvolution.Assets[1],new Vector2(7,-1),new Vector2(60,56),new Vector2(0,1),0).GetComponent<RawImage>();
            nextPortrait.color=new Color(.38f,.62f,.65f,.8f);
            nextTitle=Text("Next form",next.transform,"",12,cream,new Vector2(78,-9),new Vector2(230,20),new Vector2(0,1));
            nextProgress=Text("Next form progress",next.transform,"",10,muted,new Vector2(78,-31),new Vector2(230,17),new Vector2(0,1));
            nextFill=Track(next.transform,"Evolution",new Vector2(78,-52),new Vector2(222,2),mint);
            var tally=Panel("Run score",hud.transform,new Color(.016f,.045f,.07f,.68f));
            Place((RectTransform)tally.transform,new Vector2(0,-24),new Vector2(246,42),new Vector2(.5f,1));
            Icon("Pearl score",tally.transform,HudIcon.Symbol.Diamond,mint,new Vector2(20,-12),new Vector2(18,18),new Vector2(0,1));
            score=Text("Score",tally.transform,"",15,cream,new Vector2(48,-4),new Vector2(188,34),new Vector2(0,1),TextAnchor.MiddleCenter);
            population=Text("Sea area",hud.transform,"",11,new Color(.71f,.87f,.88f,.8f),new Vector2(0,-72),new Vector2(500,24),new Vector2(.5f,1),TextAnchor.MiddleCenter);
            status=Text("Context hint",hud.transform,"",12,cream,new Vector2(0,36),new Vector2(760,30),new Vector2(.5f,0),TextAnchor.MiddleCenter);
            var pauseButton=Button("Pause",hud.transform,"",new Vector2(-24,-24),new Vector2(44,44),ink,cream,app.TogglePause,Vector2.one);
            Icon("Pause glyph",pauseButton.transform,HudIcon.Symbol.Pause,cream,Vector2.zero,new Vector2(22,22),Vector2.one*.5f);
            var map=Panel("Minimap",hud.transform,new Color(.016f,.045f,.07f,.76f));
            Place((RectTransform)map.transform,new Vector2(-24,-82),new Vector2(116,128),Vector2.one);
            Icon("Compass",map.transform,HudIcon.Symbol.Compass,muted,new Vector2(8,-7),new Vector2(12,12),new Vector2(0,1));
            Text("Map north",map.transform,"N",8,muted,new Vector2(23,-4),new Vector2(30,17),new Vector2(0,1));
            var viewport=Panel("Camera footprint",map.transform,new Color(.55f,.86f,.89f,.08f));mapViewport=(RectTransform)viewport.transform;
            Place(mapViewport,Vector2.zero,new Vector2(20,14),Vector2.one*.5f);viewport.GetComponent<Image>().raycastTarget=false;
            mapDots=new RectTransform[GameConfig.FishCapacity];mapImages=new Image[GameConfig.FishCapacity];
            for(int i=0;i<GameConfig.FishCapacity;i++){var dot=Panel("Dot",map.transform,muted,circle);mapDots[i]=(RectTransform)dot.transform;mapImages[i]=dot.GetComponent<Image>();mapImages[i].raycastTarget=false;Place(mapDots[i],Vector2.zero,new Vector2(3,3),Vector2.one*.5f);}
            var stickPanel=Panel("Move stick",hud.transform,new Color(.025f,.10f,.14f,.26f),circle);
            var stickRect=(RectTransform)stickPanel.transform;Place(stickRect,new Vector2(34,42),new Vector2(156,156),Vector2.zero);
            Icon("Stick outer rim",stickRect,HudIcon.Symbol.Ring,new Color(.66f,.89f,.88f,.27f),Vector2.zero,new Vector2(156,156),Vector2.one*.5f);
            Icon("Stick inner rim",stickRect,HudIcon.Symbol.Ring,new Color(.66f,.89f,.88f,.09f),Vector2.zero,new Vector2(98,98),Vector2.one*.5f);
            var knob=Panel("Knob",stickRect,new Color(.37f,.68f,.70f,.50f),circle);Place((RectTransform)knob.transform,Vector2.zero,new Vector2(48,48),Vector2.one*.5f);
            knob.GetComponent<Image>().raycastTarget=false;Icon("Knob rim",knob.transform,HudIcon.Symbol.Ring,new Color(.78f,.96f,.94f,.6f),Vector2.zero,new Vector2(48,48),Vector2.one*.5f);
            var stick=stickPanel.AddComponent<VirtualStick>();stick.Knob=(RectTransform)knob.transform;controls.Bind(stick);
            var bite=ActionButton("Bite",new Vector2(-32,40),124,mint,HudIcon.Symbol.Bite,"물기",out biteFill,out biteText);
            var biteAction=bite.AddComponent<PressAction>();biteAction.Press=controls.Bite;controls.BindBite(biteAction);
            var dash=ActionButton("Dash",new Vector2(-177,51),92,new Color(.48f,.89f,.85f),HudIcon.Symbol.Dash,"돌진",out dashFill,out dashText);
            dash.AddComponent<PressAction>().Press=controls.Dash;
            fishLabels=new Text[GameConfig.FishCapacity];
            labelsRoot=Rect("Fish labels",hud.transform);Stretch(labelsRoot);labelsRoot.gameObject.AddComponent<Canvas>();
            fishHealth=new Image[GameConfig.FishCapacity];
            for(int i=0;i<GameConfig.FishCapacity;i++)
            {
                fishLabels[i]=Text("Fish tag "+i,labelsRoot,"",11,cream,Vector2.zero,new Vector2(110,22),Vector2.one*.5f,TextAnchor.MiddleCenter);
                var track=healthTracks[i]=Panel("Health",fishLabels[i].transform,new Color(.015f,.055f,.08f,.85f));
                Place((RectTransform)track.transform,new Vector2(0,-14),new Vector2(42,3),Vector2.one*.5f);track.GetComponent<Image>().raycastTarget=false;
                var hpFill=Panel("Remaining",track.transform,new Color(.36f,.92f,.7f));Stretch((RectTransform)hpFill.transform);
                fishHealth[i]=hpFill.GetComponent<Image>();fishHealth[i].raycastTarget=false;fishHealth[i].type=Image.Type.Filled;fishHealth[i].fillMethod=Image.FillMethod.Horizontal;
            }
            for(int i=0;i<damageNumbers.Length;i++)
            {
                damageNumbers[i]=Text("Damage "+i,labelsRoot,"",19,new Color(1,.71f,.43f),Vector2.zero,new Vector2(100,34),Vector2.one*.5f,TextAnchor.MiddleCenter);
                damageNumbers[i].gameObject.SetActive(false);
            }
            death=Modal("Death",new Vector2(440,300));
            Text("Death title",death.transform,"다음 파도를 타요",29,cream,new Vector2(28,-26),new Vector2(384,50),new Vector2(0,1),TextAnchor.MiddleCenter);
            deathText=Text("Death summary",death.transform,"",17,muted,new Vector2(25,-91),new Vector2(390,60),new Vector2(0,1),TextAnchor.MiddleCenter);
            respawn=Button("Respawn",death.transform,"다시 헤엄치기",new Vector2(36,-172),new Vector2(368,56),mint,ink,app.Respawn);
            respawnText=respawn.GetComponentInChildren<Text>();
            Button("Home",death.transform,"홈으로",new Vector2(145,-240),new Vector2(150,36),Color.clear,muted,app.Home);
            pause=Modal("Pause",new Vector2(440,370));
            pauseText=Text("Pause title",pause.transform,"잠깐 쉬어가기",27,cream,new Vector2(30,-25),new Vector2(380,60),new Vector2(0,1),TextAnchor.MiddleCenter);
            Button("Resume",pause.transform,"계속 헤엄치기",new Vector2(36,-105),new Vector2(368,56),mint,ink,app.TogglePause);
            Button("Sound",pause.transform,"소리 켜기 / 끄기",new Vector2(36,-177),new Vector2(368,48),new Color(.18f,.34f,.38f),cream,app.ToggleSound);
            Button("Quit",pause.transform,"홈으로",new Vector2(36,-243),new Vector2(368,48),Color.clear,muted,app.Home);
            Text("Controls reminder",pause.transform,Application.isMobilePlatform?"왼쪽 스틱 이동 · 오른쪽 버튼 물기 / 돌진":"WASD 이동   ·   SPACE 물기   ·   SHIFT 돌진",12,muted,new Vector2(30,-320),new Vector2(380,25),new Vector2(0,1),TextAnchor.MiddleCenter);
            BuildCodex();
            toast=Text("Toast",safe,"",20,cream,new Vector2(0,85),new Vector2(950,54),new Vector2(.5f,0),TextAnchor.MiddleCenter);
            death.SetActive(false);pause.SetActive(false);
            var fade=Panel("Scene transition",safe,Color.clear);Stretch((RectTransform)fade.transform);transition=fade.GetComponent<Image>();
        }
        public void SetMode(bool isMenu){menu.SetActive(isMenu);hud.SetActive(!isMenu);death.SetActive(false);pause.SetActive(false);codex.SetActive(false);FishPreviewAtlas.Ensure().Show(isMenu);cachedMass=cachedScore=cachedKills=cachedLevel=cachedForm=cachedHealth=-1;labelTimer=1;}
        public void SetTransition(float opacity){transition.color=new Color(.025f,.14f,.18f,Mathf.SmoothStep(0,1,opacity));transition.raycastTarget=opacity>0;}
        public void ShowPause(bool show,bool online){pause.SetActive(show);pauseText.text=online?"온라인 바다는 계속 흘러가요":"잠깐 쉬어가기";}
        public void Notify(string message){toast.text=message;toastTime=3;}
        public void React(WorldEvent e)
        {
            if(e.Kind==WorldEventKind.Eat&&e.Actor==app.Session.LocalId)Notify("포식 성공!  +"+e.Value.ToString("0.00")+" 성장");
            if(e.Actor==app.Session.LocalId)
            {
                if(e.Kind==WorldEventKind.Dodge)Notify("완벽 회피!  돌진 재사용 단축 · +20점");
                if(e.Kind==WorldEventKind.Combo)Notify($"연속 포식 ×{e.Value:0}  ·  점수 보너스!");
                if(e.Kind==WorldEventKind.Revenge)Notify("역전 성공!  나를 쫓던 포식자 사냥 · +100점");
                if(e.Kind==WorldEventKind.Strike)Notify("돌진 물기!");
            }
            if(e.Kind!=WorldEventKind.Damage||app.Menu)return;
            int i=damageIndex++%damageNumbers.Length;damageLife[i]=.8f;damagePositions[i]=e.Position;
            damageNumbers[i].text="−"+Mathf.RoundToInt(e.Value);damageNumbers[i].color=e.Target==app.Session.LocalId?new Color(1,.37f,.30f):new Color(1,.82f,.48f);
        }
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
            int level=GrowthCurve.Level(me.Mass,settings),form=FishEvolution.Index(GrowthCurve.Form(me.Mass,settings),me.Species);
            if(cachedMass!=mass||cachedLevel!=level||cachedForm!=form)
            {
                if(cachedLevel>0&&level>cachedLevel&&me.Alive)Notify(form!=cachedForm?"진화!  "+FishEvolution.Names[form]+"  ·  LV. "+level:"LEVEL UP  ·  LV. "+level);
                cachedMass=mass;cachedLevel=level;size.text="LV. "+level.ToString("00");
                growthText.text=FishEvolution.Names[form];
                int stage=GrowthCurve.Form(me.Mass,settings),nextStage=Math.Min(5,stage+1),nextForm=FishEvolution.Index(nextStage,me.Species);
                nextPortrait.texture=FishEvolution.Texture(nextForm);
                nextTitle.text=stage==5?"심해의 지배자":"다음 진화 · "+FishEvolution.Names[nextForm];
                float from=GrowthCurve.Threshold(stage*5+1,settings),to=GrowthCurve.Threshold(nextStage*5+1,settings);
                nextFill.fillAmount=stage==5?1:Mathf.Clamp01((me.Mass-from)/Mathf.Max(.001f,to-from));
                nextProgress.text=stage==5?"최종 외형 해금 완료":$"LV. {nextStage*5+1:00} 해금   ·   {Mathf.RoundToInt(nextFill.fillAmount*100)}%";
            }
            if(cachedForm!=form){cachedForm=form;portrait.texture=FishEvolution.Texture(form);}
            growthFill.fillAmount=Mathf.Lerp(growthFill.fillAmount,GrowthCurve.Progress(me.Mass,settings),1-Mathf.Exp(-dt*12));
            int hp=Mathf.CeilToInt(me.Health);
            if(hp!=cachedHealth){cachedHealth=hp;healthText.text=$"체력  {hp} / {settings.MaxHealth:0}";}
            healthFill.fillAmount=Mathf.Clamp01(me.Health/settings.MaxHealth);
            healthFill.color=Color.Lerp(new Color(1,.32f,.27f),new Color(.33f,.90f,.68f),Mathf.Clamp01((healthFill.fillAmount-.2f)/.6f));
            for(int d=0;d<damageNumbers.Length;d++)
            {
                damageLife[d]=Mathf.Max(0,damageLife[d]-dt);damageNumbers[d].gameObject.SetActive(damageLife[d]>0);
                if(damageLife[d]<=0)continue;
                var screenDamage=worldCamera.WorldToScreenPoint(new Vector3(damagePositions[d].X,.8f,damagePositions[d].Y+(1-damageLife[d]/.8f)*.8f));
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelsRoot,screenDamage,null,out var damagePoint);
                damageNumbers[d].rectTransform.anchoredPosition=damagePoint;var color=damageNumbers[d].color;color.a=Mathf.Min(1,damageLife[d]*4);damageNumbers[d].color=color;
            }
            if(cachedScore!=me.Score||cachedKills!=me.Kills){cachedScore=me.Score;cachedKills=me.Kills;score.text=$"{me.Score:N0}    ·    포식 {me.Kills}";}
            labelTimer+=dt;bool updateText=labelTimer>=.10f;if(updateText)labelTimer=0;
            if(updateText)
            {
                int humans=0;foreach(var f in session.Fish)if(f.Occupied&&!f.IsBot)humans++;
                string area=me.Position.Y>22?"해초 숲":me.Position.Y< -22?"심해 열수 지대":me.Position.X>20?"산호 정원":"가라앉은 유적";
                population.text=area+(session.IsOnline?$"   /   온라인 {humans}명":"   /   솔로 탐험");
            }
            status.text=!me.Alive?"":me.Protection>0?"시작 보호 · 먼저 물면 해제됩니다":me.Health<settings.MaxHealth*.4f?"체력이 낮습니다 · 진주를 먹거나 포식자를 피해 회복하세요":"";
            biteFill.fillAmount=Mathf.Clamp01(me.BiteCooldown/settings.BiteCooldown);dashFill.fillAmount=Mathf.Clamp01(me.DashCooldown/settings.DashCooldown);
            if(updateText){biteText.text=me.BiteCooldown>.05f?me.BiteCooldown.ToString("0.0"):"물기";dashText.text=me.DashCooldown>.05f?me.DashCooldown.ToString("0.0"):"돌진";}
            death.SetActive(!me.Alive&&!app.Paused);
            if(!me.Alive){deathText.text=$"{me.Score:N0} 점  ·  {me.Kills} 마리 포식\n이번 바다에서 한 뼘 더 자랐어요.";respawn.interactable=me.RespawnRemaining<=0;respawnText.text=me.RespawnRemaining>0?$"{Mathf.CeilToInt(me.RespawnRemaining)}초 후 다시 도전":"다시 헤엄치기";}
            mapViewport.anchoredPosition=new Vector2(me.Position.X,me.Position.Y)/settings.WorldHalfSize*47;
            mapViewport.sizeDelta=new Vector2(worldCamera.orthographicSize*worldCamera.aspect,worldCamera.orthographicSize)/settings.WorldHalfSize*94;
            int shown=0;float uiScale=Screen.height/810f;
            for(int pass=0;pass<session.Fish.Length;pass++)
            {
                int i=pass==0?local:pass<=local?pass-1:pass;
                var f=session.Fish[i];bool active=f.Occupied&&f.Alive;
                mapDots[i].gameObject.SetActive(active);
                if(active){mapDots[i].anchoredPosition=new Vector2(f.Position.X,f.Position.Y)/settings.WorldHalfSize*47;mapImages[i].color=i==local?cream:f.Mass>=me.Mass*settings.ConsumeRatio?new Color(1,.48f,.43f):mint;}
                bool presented=ocean.TryGetLabelPosition(i,out var labelPosition);
                var screen=worldCamera.WorldToScreenPoint(labelPosition);screen.x=Mathf.Round(screen.x);screen.y=Mathf.Round(screen.y);
                bool visible=active&&presented&&me.Alive&&!app.Paused&&screen.z>0&&screen.x>0&&screen.x<Screen.width&&screen.y>100*uiScale&&screen.y<Screen.height-100*uiScale;
                var bounds=new Rect(screen.x-55*uiScale,screen.y-14*uiScale,110*uiScale,36*uiScale);
                for(int k=0;k<shown&&visible;k++)if(occupiedLabels[k].Overlaps(bounds))visible=false;
                fishLabels[i].gameObject.SetActive(visible);
                if(!visible)continue;
                occupiedLabels[shown++]=bounds;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(labelsRoot,screen,null,out var position);fishLabels[i].rectTransform.anchoredPosition=position;
                string tag=i==local?"나":f.Protection>0?"보호":f.Mass>=me.Mass*settings.ConsumeRatio?"! 위험":PredationRules.CanConsume(me,f,settings)?"↓ 공격 가능":"비슷한 크기";
                bool rival=me.RivalRemaining>0&&me.RivalId==i&&me.RivalGeneration==f.Generation;
                if(updateText||fishLabels[i].text.Length==0)fishLabels[i].text=i==local?"▼":(rival?"◆ ":tag=="↓ 공격 가능"?"↓ ":tag=="! 위험"?"! ":"")+"LV. "+GrowthCurve.Level(f.Mass,settings).ToString("00");
                healthTracks[i].SetActive(i!=local&&f.Protection<=0);
                fishHealth[i].fillAmount=Mathf.Clamp01(f.Health/settings.MaxHealth);fishHealth[i].color=i==local?new Color(.35f,.94f,.72f):tag=="! 위험"?new Color(1,.48f,.38f):new Color(.96f,.72f,.33f);
                fishLabels[i].color=tag=="! 위험"?new Color(1,.66f,.55f):tag=="↓ 공격 가능"?mint:cream;
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
            codex=Modal("Growth codex",new Vector2(1000,610));
            codex.GetComponent<Image>().color=new Color(.025f,.075f,.115f,1);
            Text("Codex title",codex.transform,"작은 물고기에서, 심해의 지배자로",27,cream,new Vector2(30,-22),new Vector2(900,50),new Vector2(0,1));
            Text("Codex intro",codex.transform,"30레벨 · 두 계열의 물고기 12종. 상대 체력을 깎아 포식하고 진주로 성장·회복합니다.",15,muted,new Vector2(32,-82),new Vector2(936,45),new Vector2(0,1));
            for(int i=0;i<6;i++)
            {
                var card=Panel("Form "+i,codex.transform,new Color(.055f,.18f,.23f,.98f));
                Place((RectTransform)card.transform,new Vector2(30+i*158,-153),new Vector2(150,330),new Vector2(0,1));
                Text("Range",card.transform,$"LV. {i*5+1:00} — {i*5+5:00}",13,mint,new Vector2(8,-9),new Vector2(134,27),new Vector2(0,1),TextAnchor.MiddleCenter);
                evolutionPreviews[i]=PaintedImage("Evolution "+i,card.transform,FishEvolution.Assets[i],new Vector2(0,-28),new Vector2(148,140),new Vector2(.5f,1),0).GetComponent<CartoonPortrait>();
                Text("Form name",card.transform,FishEvolution.Names[i],12,cream,new Vector2(6,-151),new Vector2(138,24),new Vector2(0,1),TextAnchor.MiddleCenter);
                evolutionPreviews[i+6]=PaintedImage("Evolution alternate "+i,card.transform,FishEvolution.Assets[i+6],new Vector2(0,-165),new Vector2(148,140),new Vector2(.5f,1),0).GetComponent<CartoonPortrait>();
                Text("Alternate name",card.transform,FishEvolution.Names[i+6],12,cream,new Vector2(6,-289),new Vector2(138,24),new Vector2(0,1),TextAnchor.MiddleCenter);
                float mass=GrowthCurve.Threshold(i*5+1,app.Config);
                Text("Form scale",card.transform,$"질량 {mass:0.00}× 부터",10,muted,new Vector2(6,-312),new Vector2(138,18),new Vector2(0,1),TextAnchor.MiddleCenter);
            }
            Button("Preview bite",codex.transform,"공격 동작 느리게 보기",new Vector2(240,-520),new Vector2(250,54),mint,ink,()=>{foreach(var preview in evolutionPreviews)preview.Attack();});
            Button("Close codex",codex.transform,"닫기",new Vector2(510,-520),new Vector2(250,54),new Color(.15f,.30f,.35f),cream,()=>codex.SetActive(false));
            codex.SetActive(false);
        }
        private Image Track(Transform parent,string name,Vector2 position,Vector2 dimensions,Color color)
        {
            var track=Panel(name+" track",parent,new Color(.19f,.32f,.36f,.55f));Place((RectTransform)track.transform,position,dimensions,new Vector2(0,1));track.GetComponent<Image>().raycastTarget=false;
            var fill=Panel(name+" fill",track.transform,color);Stretch((RectTransform)fill.transform);var image=fill.GetComponent<Image>();
            image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.raycastTarget=false;return image;
        }
        private HudIcon Icon(string name,Transform parent,HudIcon.Symbol symbol,Color color,Vector2 position,Vector2 dimensions,Vector2 anchor)
        {
            var rect=Rect(name,parent);Place(rect,position,dimensions,anchor);var icon=rect.gameObject.AddComponent<HudIcon>();
            icon.Shape=symbol;icon.color=color;icon.raycastTarget=false;return icon;
        }
        private GameObject ActionButton(string name,Vector2 position,float diameter,Color accent,HudIcon.Symbol symbol,string caption,out Image cooldown,out Text label)
        {
            var button=Panel(name,hud.transform,new Color(.015f,.052f,.075f,.92f),circle);
            Place((RectTransform)button.transform,position,new Vector2(diameter,diameter),new Vector2(1,0));
            var inset=Panel("Inset",button.transform,new Color(accent.r,accent.g,accent.b,.12f),circle);
            Place((RectTransform)inset.transform,Vector2.zero,Vector2.one*(diameter-14),Vector2.one*.5f);inset.GetComponent<Image>().raycastTarget=false;
            cooldown=Cooldown(button.transform);
            Icon("Accent rim",button.transform,HudIcon.Symbol.Ring,accent,Vector2.zero,Vector2.one*(diameter-2),Vector2.one*.5f);
            Icon("Action glyph",button.transform,symbol,accent,new Vector2(0,diameter*.10f),Vector2.one*diameter*.43f,Vector2.one*.5f);
            label=Text("Action caption",button.transform,caption,12,accent,new Vector2(0,-diameter*.27f),new Vector2(diameter,24),Vector2.one*.5f,TextAnchor.MiddleCenter);
            return button;
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
