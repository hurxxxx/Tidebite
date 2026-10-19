using System;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace Tidebite
{
    public sealed partial class GameUI
    {
        private GameObject accountPanel,securityPanel,signInGroup,signedInGroup;
        private InputField accountLogin,accountPassword,accountName,profileName,currentPassword,newPassword;
        private Text accountMessage,accountSummary,accountRows,accountTabTitle,avatarLabel,securityMessage;
        private RawImage accountPortrait;
        private RectTransform accountContent;
        private ScrollRect accountScroll;
        private string profileId,accountTab="week",rowsCache;
        private int profileAvatar,deleteStage;
        private readonly System.Collections.Generic.List<Button> accountActions=new System.Collections.Generic.List<Button>();
        public void ShowAccount()
        {
            if(accountPanel==null)return;
            accountPanel.SetActive(true);accountPanel.transform.SetAsLastSibling();app.Account.Refresh(accountTab=="all"?"all":"week");RefreshAccount();
        }
        private void BuildAccount()
        {
            accountPanel=Modal("Online account",new Vector2(1040,660));
            Text("Account title",accountPanel.transform,"나의 바다 계정",27,cream,new Vector2(30,-22),new Vector2(700,44),new Vector2(0,1));
            accountMessage=Text("Account message",accountPanel.transform,"",13,muted,new Vector2(30,-70),new Vector2(980,44),new Vector2(0,1));
            signInGroup=Panel("Sign in",accountPanel.transform,Color.clear);Place((RectTransform)signInGroup.transform,new Vector2(30,-125),new Vector2(300,470),new Vector2(0,1));
            accountLogin=AccountInput(signInGroup.transform,"아이디 · 영문/숫자/밑줄 3~24자",0,24,false);
            accountPassword=AccountInput(signInGroup.transform,"비밀번호 · 영문 기준 12자 이상",85,128,true);
            accountName=AccountInput(signInGroup.transform,"가입 시 별명 · 2~16자",170,16,false);
            AccountButton("Login",signInGroup.transform,"로그인",new Vector2(0,-275),new Vector2(142,46),()=>SignIn(false));
            AccountButton("Register",signInGroup.transform,"회원가입",new Vector2(154,-275),new Vector2(142,46),()=>SignIn(true));
            Text("Remember credentials",signInGroup.transform,"혼자하기는 로그인 없이 즐길 수 있어요.\n아이디와 비밀번호를 기억해 주세요.\n앱을 다시 열면 로그인해 주세요.",12,muted,new Vector2(0,-344),new Vector2(300,92),new Vector2(0,1));
            signedInGroup=Panel("Signed in",accountPanel.transform,Color.clear);Place((RectTransform)signedInGroup.transform,new Vector2(30,-125),new Vector2(300,470),new Vector2(0,1));
            accountPortrait=PaintedImage("Account portrait",signedInGroup.transform,FishEvolution.Assets[0],new Vector2(0,0),new Vector2(90,90),new Vector2(0,1),0).GetComponent<RawImage>();
            accountSummary=Text("Online summary",signedInGroup.transform,"",13,cream,new Vector2(105,0),new Vector2(195,102),new Vector2(0,1));
            profileName=AccountInput(signedInGroup.transform,"별명 수정",120,16,false);
            var avatar=AccountButton("Avatar",signedInGroup.transform,"",new Vector2(0,-210),new Vector2(296,42),()=>{profileAvatar=(profileAvatar+1)%12;RefreshAccount();});avatarLabel=avatar.GetComponentInChildren<Text>();
            AccountButton("Save profile",signedInGroup.transform,"프로필 저장",new Vector2(0,-267),new Vector2(296,43),()=>app.Account.EditProfile(profileName.text,profileAvatar));
            AccountButton("Account settings",signedInGroup.transform,"비밀번호 변경 · 회원 탈퇴",new Vector2(0,-331),new Vector2(296,40),ShowSecurity);
            AccountButton("Logout",signedInGroup.transform,"로그아웃",new Vector2(0,-388),new Vector2(296,40),()=>app.Account.Logout());
            foreach(var item in new[]{("week","주간 순위"),("all","전체 순위"),("records","내 경기 기록")})
            {
                string tab=item.Item1;int index=tab=="week"?0:tab=="all"?1:2;
                AccountButton("Tab "+tab,accountPanel.transform,item.Item2,new Vector2(370+index*211,-125),new Vector2(199,42),()=>{accountTab=tab;rowsCache=null;accountScroll.verticalNormalizedPosition=1;app.Account.Refresh(tab=="all"?"all":"week");});
            }
            accountTabTitle=Text("Ranking note",accountPanel.transform,"",12,muted,new Vector2(372,-179),new Vector2(630,34),new Vector2(0,1));
            var viewport=Panel("Online list",accountPanel.transform,new Color(.04f,.14f,.19f));Place((RectTransform)viewport.transform,new Vector2(370,-222),new Vector2(636,350),new Vector2(0,1));viewport.AddComponent<RectMask2D>();
            accountScroll=viewport.AddComponent<ScrollRect>();accountScroll.viewport=(RectTransform)viewport.transform;accountScroll.horizontal=false;accountScroll.movementType=ScrollRect.MovementType.Clamped;
            accountContent=Rect("Rows",viewport.transform);Place(accountContent,Vector2.zero,new Vector2(636,350),new Vector2(0,1));accountScroll.content=accountContent;
            accountRows=Text("Records and leaders",accountContent,"",15,cream,new Vector2(14,-8),new Vector2(608,340),new Vector2(0,1),TextAnchor.UpperLeft);accountRows.supportRichText=false;
            Button("Close account",accountPanel.transform,"닫기 · 계속 헤엄치기",new Vector2(370,-600),new Vector2(636,40),mint,ink,()=>{accountPassword.text=currentPassword.text=newPassword.text="";securityPanel.SetActive(false);accountPanel.SetActive(false);});
            BuildSecurity();accountPanel.SetActive(false);
        }
        private Button AccountButton(string name,Transform parent,string title,Vector2 position,Vector2 dimensions,Action action)
        {var button=Button(name,parent,title,position,dimensions,new Color(.15f,.31f,.35f),cream,action);button.GetComponentInChildren<Text>().fontSize=15;accountActions.Add(button);return button;}
        private InputField AccountInput(Transform parent,string label,float y,int max,bool password)
        {Text(label,parent,label,12,muted,new Vector2(0,-y),new Vector2(300,23),new Vector2(0,1));var input=Input(parent,new Vector2(0,-y-28),new Vector2(296,43));input.gameObject.name=label;input.characterLimit=max;if(password)input.contentType=InputField.ContentType.Password;return input;}
        private void SignIn(bool register)
        {string password=accountPassword.text;accountPassword.text="";app.Account.Authenticate(register,accountLogin.text.Trim(),password,accountName.text.Trim());}
        private void RefreshAccount()
        {
            if(accountPanel==null||!accountPanel.activeSelf)return;
            var account=app.Account;bool signed=account.SignedIn&&account.Profile!=null;
            signInGroup.SetActive(!signed);signedInGroup.SetActive(signed);accountMessage.text=account.Busy?"서버에서 불러오는 중…":account.Message;
            foreach(var button in accountActions)button.interactable=!account.Busy;
            if(signed)
            {
                var p=account.Profile;if(profileId!=p.id){profileId=p.id;profileName.text=p.displayName;profileAvatar=Mathf.Clamp(p.avatar,0,11);}
                accountSummary.text=$"{p.displayName}\n온라인 {p.games}경기 · {p.wins}승\n최고 {p.bestScore:N0}점\n포식 {p.kills} · 도움 {p.assists}";
                accountPortrait.texture=FishEvolution.Texture(profileAvatar);avatarLabel.text="초상화: "+FishEvolution.Names[profileAvatar]+"  ›";
            }
            else {profileId=null;if(securityPanel.activeSelf)securityPanel.SetActive(false);}
            accountTabTitle.text=accountTab=="records"?"최근 온라인 20경기 · 중도 퇴장 기록은 순위에서 제외":(accountTab=="week"?"매주 월요일 UTC 시작":"전체 기간")+" · 최고 경기 점수 · 상위 50명";
            var rows=new StringBuilder();int count=0;
            if(accountTab=="records"&&signed)foreach(var r in account.Records)
            {rows.Append(DateTimeOffset.FromUnixTimeSeconds(r.playedAt).ToLocalTime().ToString("MM.dd HH:mm")).Append("   ").Append(r.completed?r.rank+"위":"중도 퇴장").Append("   ").Append(r.score.ToString("N0")).Append("점\n").Append("LV. ").Append(r.level).Append("  ·  포식 ").Append(r.kills).Append(" / 도움 ").Append(r.assists).Append("\n\n");count++;}
            else if(accountTab!="records")foreach(var p in account.Leaders){rows.Append(p.rank.ToString("00")).Append("    ").Append(p.displayName).Append("    ").Append(p.bestScore.ToString("N0")).Append("점\n").Append("        ").Append(p.wins).Append("승 · ").Append(p.games).Append("경기\n\n");count++;}
            string value=count==0?(accountTab=="records"?(signed?"첫 온라인 경기를 마치면 기록이 남아요.":"로그인하면 내 기록을 볼 수 있어요."):"아직 등록된 경기 기록이 없어요."):rows.ToString();
            if(value!=rowsCache){rowsCache=value;accountRows.text=value;float height=Mathf.Max(350,count*60+20);accountContent.sizeDelta=new Vector2(636,height);accountRows.rectTransform.sizeDelta=new Vector2(608,height-16);}
            if(securityPanel.activeSelf)securityMessage.text=deleteStage>0?"탈퇴 버튼을 한 번 더 누르면 삭제됩니다.":account.Message;
        }
        private void BuildSecurity()
        {
            securityPanel=Modal("Account security",new Vector2(650,570));
            Text("Security title",securityPanel.transform,"계정 설정",26,cream,new Vector2(30,-25),new Vector2(590,44),new Vector2(0,1));
            currentPassword=AccountInput(securityPanel.transform,"현재 비밀번호",90,128,true);newPassword=AccountInput(securityPanel.transform,"새 비밀번호 · 12자 이상",180,128,true);
            AccountButton("Change password",securityPanel.transform,"비밀번호 변경",new Vector2(330,-210),new Vector2(285,44),()=>{app.Account.ChangePassword(currentPassword.text,newPassword.text);currentPassword.text=newPassword.text="";});
            securityMessage=Text("Security message",securityPanel.transform,"",13,muted,new Vector2(30,-295),new Vector2(590,64),new Vector2(0,1));
            Text("Delete notice",securityPanel.transform,"회원 탈퇴 시 계정과 온라인 경기 기록이 삭제됩니다.\n현재 비밀번호를 입력하고 탈퇴 버튼을 두 번 눌러 확인하세요.",12,muted,new Vector2(30,-369),new Vector2(590,54),new Vector2(0,1));
            AccountButton("Delete account",securityPanel.transform,"회원 탈퇴",new Vector2(30,-435),new Vector2(285,44),()=>{if(deleteStage++==0){securityMessage.text="탈퇴 버튼을 한 번 더 누르면 삭제됩니다.";return;}app.Account.DeleteAccount(currentPassword.text);currentPassword.text="";deleteStage=0;});
            Button("Close settings",securityPanel.transform,"닫기",new Vector2(330,-435),new Vector2(285,44),mint,ink,()=>{securityPanel.SetActive(false);currentPassword.text=newPassword.text="";});securityPanel.SetActive(false);
        }
        private void ShowSecurity(){currentPassword.text=newPassword.text="";deleteStage=0;securityPanel.SetActive(true);securityPanel.transform.SetAsLastSibling();}
    }
}
