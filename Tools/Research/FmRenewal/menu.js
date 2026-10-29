	function kind_submit(form){
		
		form.submit();
	}
	
	function BlockMsg(){
		alert('회원님의 아이디 블럭에 의해서 요청하신 작업을 수행할 수 없습니다.\n\n자세한 사항은 마이페이지의 경고상태 확인란을 참고하시기 바랍니다.')
		return false;
	}

	function AuctionEnd(){
		alert('이미 종료된 경매입니다. 더이상 참여하실 수 없습니다.')
		return false;
	}

	function Auctionreceive(){
		alert('경매에 등록한 아이디와 참여자 아이디가 같아 참여하실 수 없습니다.')
		return false;
	}
	

	function NotCount(){
		alert('현재 보유 수량이 없거나,구매할 수 없는 카드 입니다.')
		return false;
	}	
	
	function Notlogin(refurl){
		document.location.href="/loginform.asp?loginURL=" + refurl;

	}		
	
	function BuyDeck() {
		if (!confirm("선택하신 덱을 구매 하시겠습니까?")){
				return false;
		}
		return true;
	} 	
	
	function MoveList(strURL){
		if (strURL==0) {
			document.pform.action="./list.asp"
		}
		else{
			document.pform.action="./sell_list.asp"
		}
			
		document.pform.submit();
	}
	
	function MoveList2(strURL){
		if (strURL==0) {
			document.pform.action="./list2.asp"
		}
		else{
			document.pform.action="./sell_list.asp"
		}
			
		document.pform.submit();
	}
	function MoveList3(strURL) {
	    if (strURL == 0) {
	        document.pform.action = "./Helperlist.asp"
	    }
	    else {
	        document.pform.action = "./sell_list.asp"
	    }

	    document.pform.submit();
	}
	
	function CardView(card_kind,card_no){
		document.form.card_kind.value=card_kind;
		document.form.card_no.value=card_no;
		document.form.action="./CardView.asp"
		document.form.submit();
	}	
	
	function RankView(card_kind,card_no){
		document.form.card_kind.value=card_kind;
		document.form.card_no.value=card_no;
		document.form.action="./RankView.asp"
		document.form.submit();
	}
	function CardView2(card_kind,card_no){
		document.form.card_kind.value=card_kind;
		document.form.card_no.value=card_no;
		document.form.action="./CardView2.asp"
		document.form.submit();
	}
	function HelperView(card_kind, card_no) {
	    document.form.card_kind.value = card_kind;
	    document.form.card_no.value = card_no;
	    document.form.action = "./CardViewHelper.asp"
	    document.form.submit();
	}	
	function CardComment(){
		document.pform.action="./Comment.asp"
		document.pform.submit();
	}
	function CardComment2(){
		document.pform.action="./Comment2.asp"
		document.pform.submit();
	}
	function RankCardComment(){
		document.pform.action="./RankComment.asp"
		document.pform.submit();
	}
	function HelperCardComment() {
	    document.pform.action = "./HelperComment.asp"
	    document.pform.submit();
	}	
	var win=null;
	function NewWindow(mypage,myname,w,h,scroll){
		LeftPosition=(screen.width - w) / 2;
		TopPosition=(screen.height - h) / 2;
		
		settings='height='+h+',width='+w+',top='+TopPosition+',left='+LeftPosition+',scrollbars='+scroll+',menubar=no,resizable=no,status=no';
		win=window.open(mypage,myname,settings);
		    
	  if(win.focus){win.focus();}
	}

	function entcount(){
		alert('임대수량이 부족합니다.')
		return;
		}
	function NotLentReturn(enddate) {
		alert('임대덱을 사용 중인 유저가 있습니다.\n\n'+ enddate +' 분 이후에 회수 하시기 바랍니다.')
		return;
		}
	function lentcount(){
		alert('임대수량이 부족하여 임대를 할수 없습니다.')
		return;
		}
	function lentend(){
		alert('임대가 중지된 댁은 임대를 할수 없습니다.')
		return;
		}
		
	function MarketBuyCard(card_kind,card_no){
		var Left_P;
		var Top_P;
		Left_P = (screen.width - 380) / 2;
		Top_P  = (screen.height - 560) / 2;
		
		var settings = 'toolbar=0,location=0,directories=0,status=0,menubar=0,scrollbars=0,resizable=0,width=380,height=560,top='+Top_P+',left='+Left_P;
		new_buy_window = window.open('','buycard',settings);
		buyform.card_kind.value=card_kind;
		buyform.card_no.value=card_no;
		buyform.action = "./buycard.asp";
		buyform.target = "buycard";
		buyform.submit();	
	}