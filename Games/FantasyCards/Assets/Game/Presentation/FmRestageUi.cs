using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private static readonly Color RestageEnemy = new Color(.96f,.43f,.37f);
        private static readonly Color RestageOwn = new Color(.34f,.83f,.82f);
        private bool restageSkipPackAnimation;

        private TMP_Text RestageText(Transform parent,string value,float x,float y,float xx,float yy,float size,Color tint,string role,bool heading=false,TextAlignmentOptions alignment=TextAlignmentOptions.MidlineLeft)
        {
            var text=Text(parent,value,x,y,xx,yy,size,tint,alignment);text.name=role;
            text.font=heading?(Resources.Load<TMP_FontAsset>("Fonts/Display")??font):font;
            text.fontStyle=FontStyles.Normal;return text;
        }
        private RectTransform RestagePanel(Transform parent,float x,float y,float xx,float yy,Color tint,string name,Color? edge=null)
        {
            var panel=Panel(parent,x,y,xx,yy,tint);panel.name=name;
            var shape=panel.GetComponent<UiRoundedImage>();shape.Radius=15;
            shape.TopTint=new Color(1.1f,1.1f,1.13f);shape.BottomTint=new Color(.88f,.91f,.98f);
            shape.BorderColor=edge??new Color(.57f,.66f,.77f,.36f);shape.BorderWidth=1;
            return panel;
        }
        private void RestageHairline(Transform parent,float x,float y,float xx,Color tint)
        {var line=Panel(parent,x,y,xx,y+.002f,tint);line.name="Restage section divider";}

        private void DrawRestageTitle()
        {
            QuestCurrentScreen="title";battleScreen=false;redraw=ShowTitle;Clear();UpdateWorld();FmOriginalMusic.SetScene("login");
            Panel(root,0,0,1,1,new Color(.012f,.022f,.042f));
            var art=Resources.Load<Texture2D>("Art/FmRestage/title-sanctum")??Resources.Load<Texture2D>("Art/ModernRemaster/card-title");
            if(art!=null){ImageFrame(root,art,new Rect(0,0,1,1),0,0,1,1,new Vector2(.62f,.5f));root.GetChild(root.childCount-1).name="Restage fantasy title artwork";}
            Gradient(root,0,0,.75f,1,new Color(.008f,.019f,.039f,.98f),new Color(.008f,.019f,.039f,0),true);
            Gradient(root,0,0,1,.28f,new Color(.006f,.013f,.028f,.96f),Color.clear);
            RestagePanel(root,.025f,.035f,.975f,.965f,Color.clear,"Restage title fine frame",new Color(.88f,.77f,.51f,.38f));
            RestageText(root,"FANTASY MASTERS",.058f,.817f,.52f,.925f,50,gold,"Restage title English logotype",true).characterSpacing=2.5f;
            RestageText(root,"판타지마스터즈",.055f,.655f,.56f,.817f,72,ivory,"Remaster title logo",true);
            RestageHairline(root,.061f,.636f,.409f,new Color(.81f,.72f,.47f,.75f));
            RestageText(root,"일곱 소울, 다시 펼쳐지는 카드의 세계",.061f,.547f,.51f,.618f,26,new Color(.82f,.87f,.92f),"Restage title invitation");
            RestageText(root,"REMASTER  /  SINGLE PLAYER",.061f,.478f,.49f,.535f,20,new Color(.66f,.76f,.82f),"Restage title edition",true).characterSpacing=1.4f;
            for(int i=0;i<7;i++)ElementIcon(root,(Element)i,.059f+i*.052f,.421f,.098f+i*.052f,.474f);
            var start=Button(root,"게임 시작",.06f,.282f,.452f,.394f,EnterQuestGame,gold,31);
            start.name="Restage start journey";start.GetComponent<UiRoundedImage>().Radius=12;start.gameObject.AddComponent<UiPanelEntrance>().Delay=.04f;
            var resume=Button(root,"전투 이어하기",.06f,.188f,.452f,.263f,Continue,new Color(.035f,.075f,.109f,.96f),25,HasAnyBattleSave);
            resume.GetComponent<UiRoundedImage>().Radius=11;
            Button(root,"카드 앨범",.06f,.084f,.185f,.158f,ShowRestorationOriginalArchive,slate,23);
            Button(root,"자유 연습",.197f,.084f,.322f,.158f,ShowFreePracticeLobby,slate,23);
            Button(root,"소리 설정",.334f,.084f,.459f,.158f,()=>ShowSoundSettings(ShowTitle),slate,23);
            RestageText(root,"출전 "+CardCatalog.All.Length.ToString("N0")+"종  ·  원본 카드 "+OriginalCardArchive.All.Length.ToString("N0")+"종",.06f,.038f,.56f,.078f,18,new Color(.66f,.75f,.80f),"Restage title real collection count").GetComponent<UiCanvasTextSize>().ConfigureCard(18);
            var featured=CardCatalog.All.Where(card=>card.rarity==CardRarity.Unique||card.rarity==CardRarity.Secret).GroupBy(card=>card.rarity).Select(g=>g.Last()).Take(2).ToArray();
            for(int i=0;i<featured.Length;i++)
            {
                float x=.712f+i*.099f;var card=featured[i];
                var face=ArcanaCardFace(root,card,x,.10f+i*.025f,x+.14f,.423f+i*.025f,ArcanaCardMode.Showcase);
                face.localRotation=Quaternion.Euler(0,0,i==0?8:-6);face.name="Restage title original card "+card.id;
                face.GetComponent<Image>().raycastTarget=true;var button=face.gameObject.AddComponent<Button>();
                button.transition=Selectable.Transition.None;button.onClick.AddListener(()=>ShowDetail(card,ShowTitle));BindCardHelp(face.gameObject,card,()=>ShowDetail(card,ShowTitle));
            }
            RestageText(root,"첫 기본 덱 무료 · SP로 이어가는 수집",.60f,.047f,.945f,.103f,21,new Color(.91f,.83f,.62f),"Restage title starter invitation",false,TextAlignmentOptions.Right);
        }

        private static int RestagePackElement(FmQuestPackOffer offer) => offer.element>=0?offer.element:
            offer.deckId.Length>0?(int)ArcanaDeckElements(DemoCatalog.GetDeck(offer.deckId))[0]:-1;
        private static int RestagePackTile(FmQuestPackOffer offer)
        {int element=RestagePackElement(offer);return element>=0?5+element:offer.id=="premium"?1:offer.id=="daily-a"?2:offer.id=="daily-b"?3:offer.id=="daily-c"?4:0;}
        private Color RestagePackAccent(FmQuestPackOffer offer)
        {int element=RestagePackElement(offer);return element>=0?Color.Lerp(ElementColor((Element)element),Color.white,.20f):offer.id=="premium"?new Color(.97f,.77f,.36f):offer.id=="daily-a"?new Color(.41f,.86f,.84f):offer.id=="daily-b"?new Color(.77f,.62f,.96f):offer.id=="daily-c"?new Color(.58f,.80f,.45f):new Color(.52f,.75f,.89f);}
        private static string RestagePackCode(FmQuestPackOffer offer)
        {
            string[] elements={"LIGHT","WATER","FIRE","FOREST","METAL","EARTH","DARKNESS"};
            int element=RestagePackElement(offer);
            return element>=0?elements[element]:offer.id=="premium"?"PREMIUM":offer.dailyOneChoice?"DAILY "+offer.id.Substring(offer.id.Length-1).ToUpperInvariant():"CLASSIC";
        }
        private RectTransform RestagePackCover(Transform parent,FmQuestPackOffer offer,float x,float y,float xx,float yy,bool openingBack=false)
        {
            var bounds=Panel(parent,x,y,xx,yy,Color.clear);bounds.name="Remaster pack cover bounds "+offer.id;
            var face=new GameObject("Remaster pack cover "+offer.id,typeof(RectTransform),typeof(FmRestagePackGraphic)).GetComponent<FmRestagePackGraphic>();
            face.transform.SetParent(bounds,false);Anchor((RectTransform)face.transform,0,0,1,1);
            face.OfferId=offer.id;face.StyleKey=offer.id;face.ArtworkTile=RestagePackTile(offer);face.IsOpeningBack=openingBack;
            face.IsDeck=offer.deckId.Length>0;face.Accent=RestagePackAccent(offer);
            var fit=face.gameObject.AddComponent<AspectRatioFitter>();fit.aspectMode=AspectRatioFitter.AspectMode.FitInParent;fit.aspectRatio=face.IsDeck?.75f:.66f;
            int element=RestagePackElement(offer);if(element>=0)ElementIcon(face.transform,(Element)element,.28f,.39f,.72f,.71f);
            RestageText(face.transform,"FANTASY MASTERS",.075f,.827f,.925f,.927f,22,face.Accent,"Pack brand "+offer.id,true,TextAlignmentOptions.Center).GetComponent<UiCanvasTextSize>().ConfigureCard(22);
            RestageText(face.transform,RestagePackCode(offer),.08f,.182f,.92f,.303f,25,Color.white,"Pack edition "+offer.id,true,TextAlignmentOptions.Center).GetComponent<UiCanvasTextSize>().ConfigureCard(25);
            RestageText(face.transform,(face.IsDeck?"STARTER DECK":offer.dailyOneChoice?"DAILY GIFT":"BOOSTER PACK")+"  /  "+offer.cardCount,.075f,.078f,.925f,.175f,18,face.Accent,"Pack contents count "+offer.id,false,TextAlignmentOptions.Center).GetComponent<UiCanvasTextSize>().ConfigureCard(18);
            return bounds;
        }

        private float RestageFactionHeaderHeight => Mathf.Max(.024f,14f/Mathf.Max(1,canvas.pixelRect.height));
        private float RestageLaneTop(int owner) => (owner==1?.910f:.615f)-RestageFactionHeaderHeight-.004f;
        private float RestageLaneCenterY(int owner) => ((owner==1?.660f:.365f)+RestageLaneTop(owner))*.5f;

        private void DrawRestageFactionHeaders(Transform parent=null)
        {
            if(parent==null)parent=root;
            bool small=canvas.pixelRect.height<600;
            void Header(int owner,float bottom,float top)
            {
                Color accent=owner==0?RestageOwn:RestageEnemy;
                var panel=RestagePanel(parent,.025f,bottom,.805f,top,new Color(owner==0?.025f:.095f,.039f,.067f,.98f),"Faction header "+owner,accent);
                var stripe=Panel(panel,0,0,.006f,1,accent);stripe.name="Faction edge "+owner;
                string label=owner==0?"내 전열":"상대 AI 전열";
                var text=RenewalValueText(panel,label,.018f,.02f,.34f,.98f,small?20:21,accent,"Faction identity "+owner);
                text.alignment=TextAlignmentOptions.MidlineLeft;
                var player=BattleDisplayState.players[owner];
                var count=RenewalValueText(panel,"필드 "+(player.units.Count+player.preparedSpells.Count)+"/"+BattleDisplayState.rules.unitLanes+" · 덱 "+player.deck.Count+" · 소각 "+player.discard.Count,.36f,.02f,.98f,.98f,19,new Color(.85f,.89f,.92f),"Faction public state "+owner);
                count.alignment=TextAlignmentOptions.MidlineRight;
            }
            Header(1,.910f-RestageFactionHeaderHeight,.910f);Header(0,.615f-RestageFactionHeaderHeight,.615f);
        }
    }
}
