# Galmuri11 — 원작 분위기용 대체 글꼴 후보

2026-11-11 공식 [quiple/galmuri 저장소](https://github.com/quiple/galmuri)와 [최신 릴리스 v2.40.4](https://github.com/quiple/galmuri/releases/tag/v2.40.4)를 조회했다. 릴리스 게시 시각은2026-07-12T11:54:15Z이고 태그의 실제 커밋은 `bdb86ae89466a361eb8df861222736b81ff975ef`다. 원본 응답과 각각의 시각·URL·HTTP 상태·바이트·SHA256을 이 폴더의 `.capture.json`에 보존했다.

이 글꼴은 **SIL OFL1.1로 사용할 수 있는 대체 후보**다. Fantasy Masters 원작에서 사용한 글꼴이라는 증거가 없다. 공식 README는 Nintendo DS에서 영감을 받은 디자인으로 설명하며 Nintendo와 무관한 프로젝트라고 명시한다. 원작 클라이언트의 정확한 폰트 파일/이름은 미확인이다.

## 확보한 원본 TTF

| 파일 | 종류 | 크기 | SHA256 |
|---|---|---:|---|
| `Galmuri11-v2.40.4.ttf` | 픽셀 모양의 실제 TrueType 윤곽선 | 5,376,428B | `2c709890595668f7bdb6df408420fda957dde0288e95b31a1cc17a2ab98b4b4f` |
| `Galmuri11Bitmap-Regular-2.40.3.ttf` | 실제 내장 비트맵 대안 | 783,424B | `14fecc36295eee357809540469c8554b32cc070680e1957c214531d317cbef11` |

정규 TTF의 실제 공식 원본은 `https://raw.githubusercontent.com/quiple/galmuri/bdb86ae89466a361eb8df861222736b81ff975ef/dist/Galmuri11.ttf`다. 릴리스 태그는 v2.40.4지만 **이 파일 내부 버전은 Version2.403**이고 폰트 이름은 `Galmuri11 Regular`/`Galmuri11-Regular`다. 최신 릴리스의 변경 설명은 G9/GM9에 대한 것이므로 이름을 임의의2.40.4 내부 버전으로 바꾸지 않았다.

두 파일 모두 새 HTTP200으로 받은 원본 그대로다. 정규 TTF의 계산한 Git blob SHA1은 `838b431b201bb80569c5f43a6836cf08627ca7a6`, 비트맵 TTF는 `815612ed66e5157b1ff29c16b99ca9d48041196f`로 공식 릴리스 dist API 목록과 각각 일치한다. Git 프로그램을 실행한 것은 아니다. 약19.9MB짜리 전체 릴리스 ZIP은 내려받지 않았다.

정규 TTF는 `glyf`4,928,840B이고 A/가 글리프에 각각6개 윤곽선이 있다. 작은 Bitmap TTF는 `glyf`84B뿐이며 A/가의 윤곽선 수가0이고, EBLC/EBDT에16×16/1bit 스트라이크가 하나 들어 있다. **작은 파일을 정규 윤곽선 폰트와 동일한 Unity 지원으로 간주하지 않는다.** 제품 채택·Unity 임포트·렌더링 판단은 Root의 실제 화면 검토에 남긴다.

## 실제 cmap 정적 분석

`Galmuri11-static-inspection.json`은 설치돼 있던 fontTools로 원본 TTF 테이블만 읽은 결과다. 두 파일 모두 다음과 같다.

- 총 글리프20,968개, 최선 Unicode cmap 매핑20,965개.
- 현대 한글 완성형 U+AC00–U+D7A3 **11,172/11,172개**.
- 호환 자모 U+3131–U+318E **94/94개**.
- 원작 UI에서 필요한 게임접속·아이디·비밀번호·인증번호·덱에디터·상점·내정보·저장·취소 등 표본, 숫자/영문/%/로마 숫자Ⅰ–Ⅴ 모두 매핑된다.
- 현대 결합 자모 U+1100계열의67자는 **0/67개**다. 분해된 NFD 문자열에 대한 지원을 완성형 NFC 지원과 혼동하지 않는다.

정규 TTF의 unitsPerEm은1200, 비트맵 대안은1000이다. 공식 README의 Galmuri11 권장 크기는12px/9pt다. 글리프가 존재한다는 정적 결과만으로 현재 Unity/브라우저의 선명도·배율·줄바꿈·입력 렌더링이 검증됐다고 하지 않는다.

첫 이름표 조회에서 fontTools가 레거시 Macintosh Korean 이름 레코드를 디코딩하지 못했다. 재분석은 해당2개 레코드의 원래 바이트·오류를 JSON에 보존하고 지원되는 Unicode 이름표/cmap을 읽었다. 폰트 바이트를 수정하거나 실패를 제품 런타임 결과로 바꾸지 않았다.

## 원본 허가와 동봉 조건

`ofl-v2.40.4.md`는 같은 릴리스의 **영어 SIL Open Font License1.1 원문**이다(4,266B, SHA256 `9a9e5a342c430c3fcf01a408b680f4405d5bf4ac659c931be35f8a1b27ea69c9`). 저작권은 `Copyright ©2019–2025 Lee Minseo (quiple@quiple.dev)`다. 이 원문 머리말에는 별도 Reserved Font Name 선언이 없다.

소프트웨어에 글꼴을 동봉·임베드할 수 있다. 제품이 이 후보를 채택할 때에는 저작권 고지와 영어 OFL 원문을 함께 제공하고, 글꼴/글꼴 파생물의 OFL을 유지한다. 글꼴 자체만 판매하거나 저자의 보증을 암시하면 안 된다. 한글 번역만으로 영어 원문을 대체하지 않는다. 제품 전체를 OFL로 바꾸는 조건은 아니다.

`README-v2.40.4.md`는 원본 설명을 보존한다(6,189B, SHA256 `5edc64d9684d42bf71c9ec1759ee0f61b725b5a9244f3d10aa6e4d332a367601`). 제품 파일로 복사하거나 글꼴을 설치하지 않았다. FontForge·글꼴 생성/변환·테스트·Unity·게임·브라우저·Git을 실행하지 않았다.
