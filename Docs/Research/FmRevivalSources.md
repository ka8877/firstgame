# 판타지마스터즈 시작·로비·음악 원작 자료 조사

확인일: 2026-10-26 UTC. 사용자가 기존 캐릭터·타이틀·음악을 전면 교체하고 과거 판타지마스터즈를 기준으로 다시 만들도록 요청한 작업의 자료다. 공개 자료에서 확인한 범위와 실제 원작 프로그램 복구를 구분한다.

## 실제 원작 인터페이스

[2005년 공식 온라인 인터페이스 안내](https://web.archive.org/web/20050807002655id_/http://www.fantasymasters.co.kr:80/Guide/Manual/tutorial2_1.asp)를 읽고 이 페이지가 참조한 원작 게임 화면 세 장을 직접 확인했다. HTML은 2005년 보존본이지만 해당 이미지 요청은 2006-05-21 이미지 보존본으로 이동했다. 특정 한 해의 클라이언트 버전 전체가 동일하다고 단정하지 않는다.

| 자료 | 원작 확인 내용 | 보존 파일 |
|---|---|---|
| [로비](https://web.archive.org/web/20060521215743id_/http://www.fantasymasters.co.kr/FM/images/interface_lobby.gif) | 검정·갈색 가죽·동제 장식, Room/Waiting/Chat 탭, 좌측 방 목록과 하단 채팅, 우측 접속자·프로필·보라색 구체, Tutorial/Replay/Deck Edit/Card Mall/Exit | `Tools/Research/FmRevival/interface_lobby.gif` |
| [대기방](https://web.archive.org/web/20060521215754id_/http://www.fantasymasters.co.kr/FM/images/interface_gameroom.gif) | 게임 시작 전 플레이어와 방 설정을 보여주는 별도 화면 | `interface_gameroom.gif` |
| [전장](https://web.archive.org/web/20060521215713id_/http://www.fantasymasters.co.kr/FM/images/interface_game.gif) | 초록색 대리석 전장, 5칸 전열 두 줄, 오른쪽 소울·코인·덱 정보, 아래 6장 손패와 Shuffle/Ready/Option | `interface_game.gif` |

세 장 모두 571×445픽셀의 설명용 스크린샷이다. 숫자 안내 표식이 들어 있으므로 원본 전체를 배경에 붙인 것을 기능 재현이라고 소개해서는 안 된다. 새 런타임에서는 실제 방 선택·덱·전투 기능을 구현하고, 숫자가 없는 원작 로고·장식 부분을 UV로 읽을 수 있다. 전체 원본 GIF와 1프레임 RGBA PNG의 픽셀은 동일하다. 출처·리다이렉트·해시는 `official-ui-manifest.json`, Unity 디코딩 결과는 `unity-source-images.json`에 기록했다. `prepare-original-ui.py`를 다시 실행해 PNG를 재현할 수 있다.

## 원작 웹 로그인 그림

원작 사이트의 `FM/images/login_img_01/02/04/06/08.gif`도 2005-10-30 보존본에서 확인했다. `01`에는 원작 Fantasy Masters 로고가 있고 `02/04/06/08`은 보라색 머리 여성과 용 기둥의 한 그림을 나눠 표시한 원본 조각이다. 원본 비율은 각각 233×123, 233×113, 233×82, 233×82다. 이들은 **공식 웹사이트 로그인 창**의 자료다. 클라이언트 실행 시의 실제 타이틀 구성이라고 확인한 자료는 아니다.

PNG 다섯 장은 원본을 손대지 않고 포맷만 디코딩했다. 런타임에서 원래 순서로 배치할 수 있지만, 합성·보정된 새 일러스트를 원작이라고 내세우지 않는다. 출처와 해시는 `official-login-assets.json`에 있다.

## 원작 BGM의 실제 제작사 포트폴리오

[SOUND-I의 공개 SoundCloud 앨범](https://soundcloud.com/sound-i2/sets/eomgh4mkqbec) 제목은 `(주)제오닉스 "판타지 마스터즈"`이며 처음 다섯 트랙의 실제 페이지 메타데이터와 나머지 두 트랙의 공식 oEmbed 응답을 읽었다.

| 공개 제목 | SoundCloud track ID | 확인한 범위 |
|---|---:|---|
| [1 BGM Deck](https://soundcloud.com/sound-i2/1-bgm-deck) | 234910362 | 34.980초, 덱용 제목 |
| [2 BGM Login](https://soundcloud.com/sound-i2/2-bgm-login) | 234910360 | 38.299초, 로그인용 제목 |
| [3 BGM Lobby](https://soundcloud.com/sound-i2/3-bgm-lobby) | 234910359 | 67.506초, 로비용 제목 |
| [4 BGM Market](https://soundcloud.com/sound-i2/4-bgm-market) | 234910357 | 46.815초, 상점용 제목 |
| [5 BGM Tutorial](https://soundcloud.com/sound-i2/5-bgm-tutorial) | 234910356 | 40.075초, 튜토리얼용 제목 |
| [6 BGM Game1](https://api.soundcloud.com/tracks/234910353) | 234910353 | 앨범 ID와 공식 oEmbed의 Game1 제목 확인 |
| [7 BGM Game2](https://api.soundcloud.com/tracks/234910351) | 234910351 | 앨범 ID와 공식 oEmbed의 Game2 제목 확인 |

실제 확인한 처음 다섯 트랙은 `downloadable:false`, `license:all-rights-reserved`다. 이번 도입은 음원을 추출해 로컬에 재배포하는 방식이 아니라 공식 SoundCloud 임베드로 연결한다. 네트워크와 첫 사용자 입력이 필요하다. 짧은 공개 포트폴리오의 곡이 과거 어느 패치의 전체 루프와 같은지, Game1/2가 어느 전장·속성에 연결됐는지는 확인하지 못했다. 전체 원작 OST를 복구했다고 소개하지 않는다. 정확한 링크·메타데이터는 `soundcloud-original-tracks.json`, 두 oEmbed 응답은 `soundcloud-track-234910353.json` 및 `soundcloud-track-234910351.json`에 보존했다.

## 원작 클라이언트·소스와 팬 구현 조사

- [KimYoungHwan5521/FantasyMasters](https://github.com/KimYoungHwan5521/FantasyMasters)의 공개 트리 177개를 확인했다. 확인된 리소스는 Mystic Woods 픽셀 캐릭터·타일과 TitleScene 등으로, 원작 판타지마스터즈의 로그인·로비·음악 에셋이나 원작 게임 소스를 찾지 못했다. 이름만으로 원작의 재현 코드로 취급하지 않으며 프로그램 코드를 가져오지 않았다.
- [팬마](https://fanma.kr/)의 실제 공개 웹 클라이언트와 카드·코인·효과 경로를 확인했다. 서버에 의존하는 별도 팬 프로그램이며 원작 공식 클라이언트 소스라는 근거가 없다. 이번 재현에서 해당 번들 프로그램을 복사하지 않았다. 공개 아바타 예시 `0_15.png`는 실제로 일반 회색 프로필 자리표시자여서 원작 인물로 사용하지 않는다.
- 원작 사이트 Wayback CDX의 `FM/images` 색인 173개에서 위 로그인 그림과 공식 안내 화면을 찾았다. Windows 실행 파일 MIME의 보존 색인 요청은 제한된 조사 시간 내 응답을 받지 못했다. 클라이언트가 어디에도 없다는 뜻이 아니라 **이번에 검증 가능한 원작 클라이언트 바이너리나 공식 소스는 확보하지 못했다**는 뜻이다.
- [FM LUNATIC](https://fmlunatic.com/) 원작 카드 DB와 공식 규칙 근거는 기존 `FantasyMastersDatabaseSources.md`와 `FantasyMastersRulesSources.md`를 이어 사용한다. 팬판의 현재 카드 수치를 원작 값으로 바꾸어 소개하지 않는다.

원작 자료의 공개 접근과 재배포 허가 여부는 다르다. 이 작업은 사용자의 명시적인 원작 자료 활용 요청을 따른다. 창작자·권리 상태를 숨기거나 원작 회사의 공식 서비스·전체 원본 복구라고 소개하지 않는다. 이번 단계의 실제 구현·검증 범위는 최종 릴리스 기록을 따른다.
