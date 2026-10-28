# 예수님의 길 2D 화면 개편: 실제 참고 원본

사용자가 제공한 두 화면 중 첫 번째는 **Ori and the Will of the Wisps**, 두 번째는 Unity의 공식 **Lost Crypt** 2D 샘플로 식별된다. 화면 배치·빛·깊이·인물 비율을 비교하는 참고 이미지이며, 스크린샷에서 원작 실행 코드나 스프라이트를 복원한 결과로 소개하지 않는다. 이번 개편은 예수님의 길에 한정한다.

## 확보한 공식 샘플

- 공식 저장소: <https://github.com/Unity-Technologies/mobileQA-LostCrypt-AutoPlay-Deterministic>
- 고정 커밋: `96015220e0f35b18b3ad390d38f97ddcc38bb514`
- Asset Store: <https://assetstore.unity.com/packages/essentials/tutorial-projects/lost-crypt-2d-sample-project-158673>
- 제작 설명: <https://unity.com/blog/games/download-new-2d-sample-project-lost-crypt>
- 로컬 원본: `/tmp/firstgame-luminous-reference/official-lostcrypt/LostCrypt`
- 재현 가능한 출처·전체 원본 해시·크기·라이선스 본문: [Reference/manifest.json](../Tools/Art/GospelLuminous/Reference/manifest.json)

저장소는 Unity 공식 조직의 모바일 QA용 샘플 변형이다. 원본 `ProjectVersion.txt`는 Unity **2019.3.12f1**, 패키지는 URP **7.3.1**·2D Animation **3.2.1**·SpriteShape **3.0.10**·Input System **1.0.0-preview.3**이다. 프로젝트 전체를 최신 Unity에 그대로 덮어쓰는 방식은 사용하지 않는다. 선택한 원화와 원래 메타데이터, 구현 기법을 현재 프로젝트에 옮긴다.

표준 Git LFS batch API는 현재 연결에서 `403 Resource not accessible by integration`을 반환했다. 동일한 공식 저장소의 공개 `media.githubusercontent.com` 주소에서 원래 LFS payload를 가져왔고, **666개·500,690,668바이트 전부**를 커밋의 원래 SHA-256 OID와 크기에 대조했다. 130바이트 LFS 포인터를 그림으로 취급하지 않았다. 환경 PNG 91개는 PNG 헤더와 실제 가로·세로도 확인했다. 이 수치는 확보한 원본 전체이며 실제 플레이어에 666개를 모두 포함한다는 뜻은 아니다.

## 사용 조건과 표시

확인한 공식 Asset Store 설명에는 다음 문장이 있다.

> All the assets can be repurposed and added to your own projects. Review the full licence for usage and redistribution terms: Unity Companion License

해당 링크는 [Unity Companion License v1.4](https://unity.com/legal/licenses/unity-companion-license)로 연결된다. 공식 저장소 루트에는 별도 LICENSE 파일이 없으므로 GitHub 공개 여부만으로 일반적인 오픈 소스나 퍼블릭 도메인이라고 주장하지 않는다. 자산의 재사용 근거는 공식 상품 설명과 그 설명이 지정한 라이선스다. 상품 페이지 공통 메타데이터의 “Standard Unity Asset Store EULA”와 실제 설명이 지정한 Companion License를 구분해서 기록했다.

Companion License는 유효한 Unity 엔진 라이선스와 연결한 콘텐츠 제작·배포에 적용된다. 복사한 상당 부분과 함께 전체 라이선스 및 원본 저작권 표시를 제공해야 한다. 실제 배포용 `SOURCE-NOTICE.txt`/라이선스 안내에 manifest의 `license.fullText`와 Unity Technologies 출처를 포함한다. 원본에 포함된 제삼자 코드의 별도 고지를 제거하지 않는다. Unity 브랜드나 Lost Crypt의 주인공을 예수님 게임의 새 창작물이라고 표시하지 않는다.

## 실제로 참고할 구성

원본 `Assets/Scripts/ParallaxLayer.cs`는 카메라의 시작 위치와 현재 위치 차이에 층별 계수를 곱해 배경을 이동한다. 프리팹에서 확인한 예는 먼 숲 `0.3`·`0.4`·`0.75`, 구름 `0.8`·`0.9`, 가까운 전경 `-0.8`이다. 이 값은 **월드 오브젝트가 카메라 이동을 따라가는 비율**이므로 일반적인 “배경 스크롤 속도”와 부호·의미를 혼동하지 않는다.

주요 실제 원화는 다음 경로에 있다. 아래 경로의 기준은 원본 `Assets/Environment`다.

| 용도 | 원본 파일 | 실제 크기 |
|---|---|---|
| 중경 나무 | `Sprites/Vegetation/Sprite_Tree_1.png` | 844 × 897 |
| 중경 나무 | `Sprites/Vegetation/Sprite_Tree_2.png` | 999 × 909 |
| 중경 나무 | `Sprites/Vegetation/Sprite_Tree_3.png` | 885 × 920 |
| 먼 숲의 덩어리 | `Sprites/Vegetation/Sprite_Trees_1.png`·`_2.png` | 1066 × 490 / 1024 × 490 |
| 가까운 줄기·가지 | `Sprites/Vegetation/Sprite_TrunkAndBranches_1_Color.png` | 2316 × 3135 |
| 가까운 줄기·가지 | `Sprites/Vegetation/Sprite_TrunkAndBranches_2_Color.png` | 1443 × 3145 |
| 잎·수관 | `Sprites/Vegetation/Sprite_Foliage_1_Color.png`·`_2_Color.png`·`_3_Color.png` | 957 × 884 / 1324 × 1099 / 1373 × 1127 |
| 석재 원경·유적 | `Sprites/Crypt/Sprite_Ruins_2_Color.png` | 1216 × 952 |
| 석주 | `Sprites/Crypt/Sprite_Pillar_1_Color.png`·`_2_Color.png` | 647 × 3040 / 391 × 2467 |
| 풀·꽃 경계 | `Sprite Shapes/Sprites/Sprite_Strip_Grass_1.png`·`Sprite_Strip_Flowers_1.png` | 4096 × 1024 |
| 빛줄기 | `Sprites/Lights/Sprite_Shafts_1.png` | 1890 × 1017 |
| 지형용 색상 시트 | `Tilemaps/Spritesheet_Tileset.png` | 2048 × 2048 |

원본 나무의 `.meta`는 100 pixels/unit, 중심 피벗, alpha transparency를 사용한다. 같은 이름의 `_Mask`는 색 원화가 아니며, `_Normal`은 2D 조명용 보조 텍스처다. 잎과 지형 atlas는 원래 `.meta`의 sprite slicing을 확인해야 한다.

원본에는 `ShaderGraph_Vegetation_Wind_Lit.shadergraph`, `ShaderGraph_Water_Unlit.shadergraph`, 2D 조명 프리팹, Volume 프리팹, 먼 숲/중경/전경 프리팹이 포함된다. 공식 상품 설명도 2D IK·SpriteShape·Tilemap·동적 2D 조명·normal/mask 보조 텍스처·Shader Graph·후처리·입자를 명시한다. 최신 URP로 이식할 때 원본의 오래된 Shader Graph나 에디터 보조 툴을 무조건 실행 가능한 것으로 취급하지 않는다.

수풀·수관이 겹치는 큰 실루엣, 여러 속도로 움직이는 먼 숲과 가까운 가지, 차가운 안개와 따뜻한 빛, 꽃·풀의 작은 밝은 점, 화면 하단에 작게 배치한 HUD를 함께 맞춘다. 원화 파일의 확보·해시 검사는 Unity의 실제 렌더링·조작 검증이나 사용자의 미적 승인을 대신하지 않는다. 성전·숲의 새 배치는 게임용 미술 표현이며 역사적 장소의 정확한 복원이라고 소개하지 않는다.
