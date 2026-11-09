using System;
using System.Collections.Generic;

namespace PathOfDawn.Core
{
    /// <summary>Stage directions for the current event, never a preview of undiscovered book pages.</summary>
    public sealed class GospelStorybookScene
    {
        public string Title { get; }
        public string Location { get; }
        public string TimeLabel { get; }
        public string ArtKey { get; }
        public string MusicRole { get; }
        public string Cue { get; }

        internal GospelStorybookScene(string title, string location, string time, string art, string music, string cue)
        { Title = title; Location = location; TimeLabel = time; ArtKey = "Art/GospelStorybook/" + art; MusicRole = music; Cue = cue; }
    }

    /// <summary>A short, sourced presentation step inside an unchanged saved event.</summary>
    public sealed class GospelStorybookDetail
    {
        public GospelDialogueTurn Turn { get; }
        public string Reference { get; }
        public GospelStorybookScene Scene { get; }
        internal GospelStorybookDetail(GospelDialogueTurn turn, string reference, GospelStorybookScene scene = null)
        { Turn = turn; Reference = reference; Scene = scene; }
    }

    /// <summary>
    /// Narrative presentation of the same twenty-three saved events. The legacy
    /// dialogue range is retained; optional short detail pages deepen the opening
    /// without reassigning an existing event or passage. Full retained passages
    /// remain in the discovery-only Bible.
    /// </summary>
    public static class GospelStorybookCatalog
    {
        public const string Revision = "gospel-nativity-2026-11-02";
        public const string Scope = "탄생 · 열두 살 성전 방문 · 요단강의 세례. 이후의 사역과 십자가·부활·승천은 다음 이야기입니다.";

        static GospelDialogueTurn Narrate(string text, string portrait = "teacher")
            => new GospelDialogueTurn("이야기", portrait, text);
        static GospelDialogueTurn Dramatic(string speaker, string portrait, string text)
            => new GospelDialogueTurn(speaker, portrait, text, false, true);

        static GospelDialogueTurn Speech(GospelReading reading, int verse, string speaker, string portrait,
            string after = null, string before = null)
        {
            if (reading == null) throw new ArgumentException("A recorded speech requires its verified passage.");
            foreach (string line in reading.Text.Split('\n'))
            {
                string prefix = verse + " ";
                if (!line.StartsWith(prefix, StringComparison.Ordinal)) continue;
                string words = line.Substring(prefix.Length);
                if (after != null)
                {
                    int start = words.IndexOf(after, StringComparison.Ordinal);
                    if (start < 0) throw new ArgumentException("The speech boundary is absent from " + reading.Id);
                    words = words.Substring(start + after.Length);
                }
                if (before != null)
                {
                    int end = words.LastIndexOf(before, StringComparison.Ordinal);
                    if (end < 0) throw new ArgumentException("The speech boundary is absent from " + reading.Id);
                    words = words.Substring(0, end);
                }
                return new GospelDialogueTurn(speaker, portrait, words, true);
            }
            throw new ArgumentException("The speech verse is absent from " + reading.Id);
        }

        public static GospelStorybookScene GetScene(GospelChronicleQuest quest)
        {
            if (quest == null) throw new ArgumentNullException(nameof(quest));
            switch (quest.Id)
            {
                case "birth-road-bethlehem": return Scene("베들레헴으로", "나사렛에서 베들레헴으로", "탄생을 앞두고", "birth-road", "birth", "길을 떠나는 발자국. 먼 동네의 불빛이 보입니다.");
                case "birth-birth-manger": return Scene("구유에 뉘인 아기", "베들레헴", "예수님의 탄생", "birth-manger", "birth", "작은 불빛 아래, 강보에 싸인 아기가 있습니다.");
                case "birth-night-shepherds": return Scene("들판의 밤", "베들레헴 부근 들판", "같은 밤", "birth-shepherds", "birth", "양 떼를 지키는 밤. 들판을 빛이 두릅니다.");
                case "birth-birth-good-news": return Scene("두려움에서 기쁨으로", "밤의 들판", "목자들에게 전해진 소식", "birth-shepherds", "birth", "놀란 목자들에게 천사의 말이 들립니다.");
                case "birth-manger-witnesses": return Scene("서둘러 찾아온 사람들", "베들레헴", "목자들의 방문", "birth-manger", "birth", "목자들이 찾아와 들었던 소식을 전합니다.");
                case "birth-birth-remembered": return Scene("마음에 간직하다", "베들레헴", "방문 뒤", "birth-manger", "birth", "목자들은 돌아가고, 마리아는 들은 말을 마음에 둡니다.");
                case "birth-name-jesus": return Scene("예수라는 이름", "아기 때", "팔 일이 되었을 때", "birth-manger", "birth", "그 이름을 예수라 하였습니다.");
                case "birth-temple-dedication": return Scene("아기를 안고 성전으로", "예루살렘 성전", "아기 때의 성전 방문", "birth-temple", "birth", "부모는 모세의 법을 따라 아기를 데리고 올라갑니다.");
                case "birth-simeon-peace": return Scene("기다리던 만남", "예루살렘 성전", "시므온의 찬송", "birth-temple", "birth", "시므온은 아기를 안고 하나님을 찬송합니다.");
                case "birth-anna-thanksgiving": return Scene("안나의 감사", "예루살렘 성전", "그 때에 나아와", "birth-temple", "birth", "안나는 감사하며 아기에 관해 말합니다.");
                case "birth-nazareth-growth": return Scene("나사렛의 날들", "갈릴리 나사렛", "성장하시는 예수님", "nazareth", "birth", "나사렛에서 예수님이 자라십니다.");
                case "nazareth-departure": return Scene("유월절 길을 준비하며", "나사렛 길목", "열두 살", "nazareth", "temple", "요셉과 함께 출발할 준비를 합니다.");
                case "pilgrim-spring": return Scene("함께 오르는 길", "예루살렘으로 향하는 길", "유월절의 여정", "nazareth", "temple", "샘가에서 마리아를 만납니다.");
                case "festival-courtyard": return Scene("절기의 예루살렘", "예루살렘 성전 뜰", "유월절", "temple", "temple", "성전을 오가는 사람들 사이로 길이 이어집니다.");
                case "listening-court": return Scene("듣고 묻는 자리", "성전 회랑", "절기가 지난 뒤", "temple", "temple", "회랑을 따라 선생들이 있는 곳으로 향합니다.");
                case "teachers-circle": return Scene("선생들 가운데", "성전 안쪽 뜰", "사흘 후 성전에서", "temple", "temple", "예수님은 선생들 가운데 앉아 계십니다.");
                case "parents-reunite": return Scene("다시 만난 부모", "예루살렘 성전", "사흘 만의 만남", "temple", "temple", "놀란 마리아가 예수님께 말합니다.");
                case "nazareth-return": return Scene("함께 나사렛으로", "나사렛으로 돌아가는 길", "성전 방문 뒤", "nazareth", "temple", "가족은 함께 내려갑니다. 소년의 날들이 이어집니다.");
                case "adult-jordan-road": return Scene("요단으로 향하다", "요단강으로 향하는 길", "공생애가 시작되는 무렵", "jordan", "baptism", "이제 성인이 된 예수님의 발걸음을 따라갑니다.");
                case "john-calling": return Scene("빈 들에서 들리는 소리", "요단 부근", "요한의 사역", "jordan", "baptism", "요한은 사람들에게 회개의 세례를 전합니다.");
                case "baptism-request": return Scene("모든 의를 이루는 길", "요단강", "요한과의 만남", "jordan", "baptism", "예수님은 요한에게 세례를 받으려 하십니다.");
                case "baptism-water": return Scene("물에서 올라오시다", "요단강", "세례를 받으신 뒤", "jordan", "baptism", "예수님은 물에서 올라오십니다. 하늘에서 소리가 들립니다.");
                case "ministry-beginning": return Scene("공생애의 문턱", "요단의 여정을 마치며", "삼십세쯤", "jordan", "baptism", "삼십세쯤 되신 예수님이 가르치심을 시작하십니다.");
                default: throw new ArgumentException("Unknown story scene: " + quest.Id);
            }
        }

        static GospelStorybookScene Scene(string title, string location, string time, string art, string music, string cue)
            => new GospelStorybookScene(title, location, time, art, music, cue);

        public static GospelDialogueTurn GetBeat(GospelChronicle story)
        {
            if (story == null) throw new ArgumentNullException(nameof(story));
            if (story.IsComplete || story.Stage != GospelQuestStage.Reading) return null;
            return GetDetail(story.CurrentQuest, story.DialogueIndex, story.StoryDetailIndex).Turn;
        }

        static GospelStorybookDetail Detail(GospelDialogueTurn turn, string reference, GospelStorybookScene scene = null)
            => new GospelStorybookDetail(turn, reference, scene);

        public static GospelStorybookDetail GetDetail(GospelChronicleQuest quest, int legacyIndex, int detailIndex)
        {
            var details = GetDetails(quest, legacyIndex);
            if (detailIndex < 0 || detailIndex >= details.Count) throw new ArgumentOutOfRangeException(nameof(detailIndex));
            return details[detailIndex];
        }

        public static int GetStoryBeatIndex(GospelChronicleQuest quest, int legacyIndex, int detailIndex)
        {
            _ = GetDetail(quest, legacyIndex, detailIndex);
            int result = detailIndex;
            for (int i = 0; i < legacyIndex; i++) result += GetDetails(quest, i).Count;
            return result;
        }

        public static int GetStoryBeatCount(GospelChronicleQuest quest)
        {
            if (quest == null) throw new ArgumentNullException(nameof(quest));
            int result = 0;
            for (int i = 0; i < quest.Dialogue.Count; i++) result += GetDetails(quest, i).Count;
            return result;
        }

        public static GospelStorybookScene GetCurrentScene(GospelChronicle story)
        {
            if (story == null) throw new ArgumentNullException(nameof(story));
            if (story.IsComplete) return null;
            return GetDetail(story.CurrentQuest, story.DialogueIndex, story.StoryDetailIndex).Scene ?? GetScene(story.CurrentQuest);
        }

        /// <summary>
        /// Legacy dialogue indices are never renumbered. Each may now contain
        /// several short, separately sourced steps. New summaries are explicitly
        /// narrative, not newly claimed exact HAN quotations or new book rewards.
        /// </summary>
        public static IReadOnlyList<GospelStorybookDetail> GetDetails(GospelChronicleQuest quest, int legacyIndex)
        {
            if (quest == null) throw new ArgumentNullException(nameof(quest));
            if (legacyIndex < 0 || legacyIndex >= quest.Dialogue.Count) throw new ArgumentOutOfRangeException(nameof(legacyIndex));
            var r = quest.Reading;
            GospelStorybookDetail[] details;
            switch (quest.Id)
            {
                case "birth-road-bethlehem":
                    if (legacyIndex == 0)
                    {
                        var maryScene = Scene("약속을 들은 마리아", "갈릴리 나사렛", "탄생을 앞둔 시절", "nativity-annunciation", "birth", "마리아 앞에 천사 가브리엘이 나타납니다.");
                        var visitScene = Scene("마리아와 엘리사벳의 만남", "유다의 산중", "마리아의 방문", "nativity-visitation", "birth", "엘리사벳은 마리아를 축복하고, 마리아는 하나님을 높입니다.");
                        var josephScene = Scene("요셉에게 전해진 소식", "요셉의 꿈", "마리아를 맞아들이기 전", "nativity-joseph", "birth", "요셉의 꿈에 주의 사자가 나타납니다.");
                        details = new[] {
                            Detail(Narrate("갈릴리 나사렛. 마리아는 다윗의 집안 사람 요셉과 정혼한 처녀였습니다.", "mary"), "누가복음 1:26–27 · 본문 요약", maryScene),
                            Detail(Dramatic("가브리엘", "teacher", "마리아여, 두려워하지 마라. 네가 아들을 낳을 것이니 그 이름을 예수라 하여라. 그분은 지극히 높으신 이의 아들이라 불릴 것이며, 그 나라는 끝이 없을 것이다."), "누가복음 1:28–35를 따른 대화 · 극화", maryScene),
                            Detail(Dramatic("마리아", "mary", "저는 주의 여종입니다. 말씀하신 대로 제게 이루어지기를 바랍니다."), "누가복음 1:38을 따른 응답 · 극화", maryScene),
                            Detail(Narrate("마리아는 유다 산중의 엘리사벳을 찾아갑니다. 엘리사벳은 주께서 하신 말씀이 이루어지리라 믿은 마리아를 축복했습니다. 마리아도 하나님을 높이며 기뻐합니다.", "mary"), "누가복음 1:39–56 · 본문 요약", visitScene),
                            Detail(Narrate("마리아의 잉태를 알게 된 요셉은 그를 드러내지 않고 가만히 끊고자 생각했습니다.", "joseph"), "마태복음 1:18–19 · 본문 요약", josephScene),
                            Detail(Dramatic("주의 사자", "teacher", "다윗의 자손 요셉아, 마리아를 맞아들이기를 두려워하지 마라. 그에게 잉태된 이는 성령으로 된 것이다. 아들의 이름을 예수라 하여라. 그가 자기 백성을 죄에서 구원할 것이다."), "마태복음 1:20–21을 따른 대화 · 극화", josephScene),
                            Detail(Narrate("요셉은 잠에서 깨어 주의 사자가 분부한 대로 마리아를 아내로 맞아들였습니다.", "joseph"), "마태복음 1:22–25 · 본문 요약", josephScene)
                        };
                    }
                    else details = new[] {
                        Detail(Narrate("가이사 아구스도가 호적 명령을 내렸습니다. 사람들은 호적하러 자기 고향으로 향합니다.", "joseph"), "누가복음 2:1–3 · 본문 요약 / 연대 단정 없음"),
                        Detail(Narrate("요셉도 다윗의 집 족속이므로, 잉태한 마리아와 함께 나사렛에서 유대로 올라갑니다. 그들이 향하는 곳은 다윗의 동네라 불리는 베들레헴입니다.", "joseph"), "누가복음 2:4–5 · 본문 요약"),
                        Detail(Dramatic("요셉", "joseph", "베들레헴으로 가야 하오. 함께 길을 떠납시다."), "누가복음 2:4–5의 여정 · 이동 대화는 극화")
                    };
                    break;
                case "birth-birth-manger": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("베들레헴에서 마리아가 해산할 날이 찼습니다. 마리아는 첫아들을 낳았습니다.", "mary"), "누가복음 2:6–7 · 본문 요약"),
                    Detail(Narrate("사관에는 머물 곳이 없었습니다. 마리아는 아기를 강보로 싸서 구유에 뉘었습니다.", "mary"), "누가복음 2:7, 12 · 본문 요약")
                } : new[] {
                    Detail(Dramatic("마리아", "mary", "이 작은 아기를 품에 안고, 조심스럽게 강보로 감싸겠어요."), "누가복음 2:7의 행동 · 대화는 극화"),
                    Detail(Narrate("같은 밤, 베들레헴 부근 들판에서는 목자들이 양 떼를 지키고 있었습니다."), "누가복음 2:7–8 · 본문 연결 해설")
                }; break;
                case "birth-night-shepherds": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("그 지경에는 목자들이 있었습니다. 그들은 밖에서 밤을 지새우며 양 떼를 지키고 있었습니다. 일하던 그 밤, 뜻밖의 만남이 찾아옵니다."), "누가복음 2:8 · 본문 요약"),
                    Detail(Narrate("주의 사자가 목자들 곁에 서고, 주의 영광이 그들을 두루 비추었습니다. 목자들은 크게 두려워했습니다. 천사는 먼저 그 두려움에 말을 건넵니다."), "누가복음 2:9–10 · 본문 요약")
                } : new[] { Detail(Dramatic("목자", "teacher", "저 빛을 보아! 무슨 일이 일어난 걸까?"), "누가복음 2:9의 반응 · 대화는 극화") }; break;
                case "birth-birth-good-news": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("크게 두려워하는 목자들 앞에서 천사가 말을 건넵니다."), "누가복음 2:10 · 본문 요약")
                } : new[] {
                    Detail(Speech(r, 10, "천사", "teacher", "천사가 이르되 "), "누가복음 2:10 · 개역한글 직접 인용"),
                    Detail(Speech(r, 11, "천사", "teacher"), "누가복음 2:11 · 개역한글 직접 인용"),
                    Detail(Dramatic("천사", "teacher", "강보에 싸여 구유에 누인 아기를 찾을 것이다. 그것이 너희에게 표적이 될 것이다."), "누가복음 2:12를 따른 대화 · 극화"),
                    Detail(Dramatic("천군", "teacher", "지극히 높은 곳에서는 하나님께 영광! 땅에서는 하나님이 기뻐하시는 사람들에게 평화!"), "누가복음 2:13–14를 따른 찬송 · 극화")
                }; break;
                case "birth-manger-witnesses": details = legacyIndex == 0 ? new[] {
                    Detail(Speech(r, 15, "목자들", "teacher", "목자가 서로 말하되 ", " 하고"), "누가복음 2:15 · 개역한글 직접 인용")
                } : new[] {
                    Detail(Narrate("목자들은 빨리 가서 마리아와 요셉, 구유에 누인 아기를 찾았습니다. 천사가 일러 준 표적대로, 강보에 싸인 아기가 그곳에 있었습니다.", "joseph"), "누가복음 2:12, 16 · 본문 요약"),
                    Detail(Narrate("목자들은 아기를 보고 천사에게 들은 말을 전했습니다. 듣는 사람들은 그 말을 기이하게 여겼습니다."), "누가복음 2:17–18 · 본문 요약")
                }; break;
                case "birth-birth-remembered": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("마리아는 이 모든 말을 마음에 지키어 생각했습니다. 찾아온 사람들의 말과 아기의 모습을 서둘러 지나치지 않고, 마음에 두었습니다.", "mary"), "누가복음 2:19 · 본문 요약"),
                    Detail(Narrate("목자들은 자기들에게 이르던 바와 같이 듣고 본 모든 것을 인하여 하나님께 영광을 돌리고 찬송하며 돌아갔습니다. 그들이 지키던 들판에도 다시 날이 이어집니다."), "누가복음 2:20 · 본문 요약 / 장면 연결 해설")
                } : new[] { Detail(Dramatic("마리아", "mary", "오늘 들은 말들을 오래 마음에 간직하겠어요."), "누가복음 2:19의 마음 · 대화는 극화") }; break;
                case "birth-name-jesus": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("할례할 팔 일이 되었습니다. 아기의 이름을 예수라 하였습니다. 수태하기 전에 천사가 일러 준 이름입니다.", "mary"), "누가복음 2:21 · 본문 요약"),
                    Detail(Dramatic("요셉", "joseph", "이 아기의 이름을 예수라 하겠소."), "누가복음 2:21 / 마태복음 1:25의 이름 짓기 · 대화는 극화")
                } : new[] { Detail(Narrate("모세의 법대로 결례의 날이 찼습니다. 부모는 아기를 데리고 예루살렘으로 올라갈 채비를 합니다.", "mary"), "누가복음 2:21–22 · 본문 연결 해설") }; break;
                case "birth-temple-dedication": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("모세의 법대로 결례의 날이 차자, 부모는 아기를 데리고 예루살렘으로 올라갔습니다.", "joseph"), "누가복음 2:21–22 · 본문 요약"),
                    Detail(Narrate("그들은 율법에 기록된 대로 첫 태에 처음 난 남자 아기를 주께 드리고자 했습니다. 아기를 안은 가족이 예루살렘 성전으로 들어갑니다.", "joseph"), "누가복음 2:22–23 · 본문 요약"),
                    Detail(Narrate("부모는 율법에 말씀하신 대로 비둘기 한 쌍이나 어린 반구 둘로 제사하려 했습니다.", "mary"), "누가복음 2:24 · 본문 요약")
                } : new[] { Detail(Dramatic("요셉", "joseph", "아기를 데리고 성전으로 갑시다. 율법에 기록된 대로 행해야겠소."), "누가복음 2:22–24의 행동 · 대화는 극화") }; break;
                case "birth-simeon-peace": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("이스라엘의 위로를 기다리던 시므온이 성령의 이끄심을 따라 성전으로 들어옵니다."), "누가복음 2:25–27 · 본문 요약"),
                    Detail(Narrate("부모가 율법의 전례대로 행하려고 아기 예수를 데리고 왔을 때, 시므온은 아기를 안고 하나님을 찬송했습니다. 오래 기다린 사람이 아기를 품에 안은 만남이었습니다."), "누가복음 2:27–28 · 본문 요약")
                } : new[] {
                    Detail(Speech(r, 29, "시므온", "teacher"), "누가복음 2:29 · 개역한글 직접 인용"),
                    Detail(Speech(r, 30, "시므온", "teacher"), "누가복음 2:30 · 개역한글 직접 인용"),
                    Detail(Speech(r, 31, "시므온", "teacher"), "누가복음 2:31 · 개역한글 직접 인용"),
                    Detail(Speech(r, 32, "시므온", "teacher", null, " 하니"), "누가복음 2:32 · 개역한글 직접 인용")
                }; break;
                case "birth-anna-thanksgiving": details = legacyIndex == 0 ? new[] {
                    Detail(Narrate("선지자 안나는 성전을 떠나지 않고 밤낮으로 금식하며 기도함으로 섬겼습니다."), "누가복음 2:36–37 · 본문 요약"),
                    Detail(Narrate("안나는 마침 그때에 나아와 하나님께 감사했습니다. 그리고 예루살렘의 구속을 바라는 모든 사람에게 이 아기에 대하여 말했습니다."), "누가복음 2:38 · 본문 요약")
                } : new[] { Detail(Narrate("부모는 주의 율법을 따라 모든 일을 마치고, 아기를 데리고 갈릴리 나사렛으로 돌아갑니다.", "joseph"), "누가복음 2:39 · 본문 요약") }; break;
                case "birth-nazareth-growth":
                    if (legacyIndex == 0)
                    {
                        var matthewScene = Scene("박사들의 방문과 피신", "베들레헴 · 애굽 · 나사렛", "탄생 뒤", "nativity-magi", "birth", "박사들이 아기를 찾아오고, 요셉은 꿈에서 피하라는 말을 듣습니다.");
                        details = new[] {
                            Detail(Narrate("동방에서 온 박사들이 아기와 마리아가 있는 집에 들어가 경배하고 예물을 드렸습니다.", "mary"), "마태복음 2:1–12 · 본문 요약", matthewScene),
                            Detail(Narrate("요셉은 꿈에서 피하라는 지시를 받았습니다. 밤에 아기와 그 어머니를 데리고 애굽으로 떠나, 헤롯이 죽기까지 그곳에 머물렀습니다.", "joseph"), "마태복음 2:13–15 · 본문 요약", matthewScene),
                            Detail(Narrate("헤롯은 베들레헴과 그 지경의 어린아이들을 죽이게 했습니다. 자식을 잃은 울음이 그곳에 남았습니다."), "마태복음 2:16–18 · 본문 요약", matthewScene),
                            Detail(Narrate("헤롯이 죽은 뒤, 요셉은 아기와 그 어머니를 데리고 돌아옵니다. 유대로 가기를 두려워한 그는 꿈의 지시를 따라 갈릴리로 물러가 나사렛에 살았습니다.", "joseph"), "마태복음 2:19–23 · 본문 요약", matthewScene),
                            Detail(Narrate("나사렛에서 아기가 자라며 강하여지고 지혜가 충족했습니다. 하나님의 은혜가 그 위에 있었습니다.", "mary"), "누가복음 2:39–40 · 본문 요약")
                        };
                    }
                    else details = new[] {
                        Detail(Narrate("나사렛에서 세월이 흘렀습니다. 예수님은 자라며 강하여지고 지혜가 충족하셨습니다."), "누가복음 2:40–42 · 본문 연결 요약"),
                        Detail(Narrate("열두 살이 된 예수님은 부모와 함께 유월절을 지키러 예루살렘으로 올라갑니다.", "joseph"), "누가복음 2:41–42 · 본문 요약")
                    };
                    break;
                default:
                    details = new[] { Detail(GetBeats(quest)[legacyIndex], ReferenceForWorldQuest(quest, legacyIndex)) };
                    break;
            }
            return Array.AsReadOnly(details);
        }

        static string ReferenceForWorldQuest(GospelChronicleQuest quest, int legacyIndex)
        {
            switch (quest.Id)
            {
                case "nazareth-departure": case "pilgrim-spring": return "누가복음 2:41–42 · 길 안내 대화는 극화";
                case "festival-courtyard":
                    return legacyIndex == 0 ? "누가복음 2:41–42의 순례 · 길 안내 대화는 극화" :
                        legacyIndex == 1 ? "누가복음 2:42–43 · 본문 요약" : "누가복음 2:43–46 · 본문 연결 요약";
                case "listening-court": return "누가복음 2:46 · 길 안내 대화는 극화";
                case "teachers-circle":
                    return legacyIndex == 0 ? "누가복음 2:46 · 본문 요약" :
                        legacyIndex == 1 ? "누가복음 2:47의 반응 · 대화는 극화" : "누가복음 2:46, 48 · 본문 요약";
                case "parents-reunite":
                    return legacyIndex < 2 ? "누가복음 2:" + (48 + legacyIndex) + " · 개역한글 직접 인용" :
                        legacyIndex == 2 ? "누가복음 2:50 · 본문 요약" : "누가복음 2:51 · 본문 요약";
                case "nazareth-return":
                    return legacyIndex == 0 ? "누가복음 2:51의 귀향 · 대화는 극화" :
                        legacyIndex == 1 ? "누가복음 2:51–52 · 본문 요약" : "마태복음 3:13 · 본문 연결 요약";
                case "adult-jordan-road": return "마태복음 3:13 · 길 안내 대화는 극화";
                default: return quest.Reading != null ? quest.Reading.Reference : "복음서의 기록을 따른 이야기";
            }
        }

        public static IReadOnlyList<GospelDialogueTurn> GetBeats(GospelChronicleQuest quest)
        {
            if (quest == null) throw new ArgumentNullException(nameof(quest));
            var r = quest.Reading;
            GospelDialogueTurn[] turns;
            switch (quest.Id)
            {
                case "birth-road-bethlehem": turns = new[] {
                    Narrate("호적 명령이 내려졌습니다. 사람들은 자기 고향으로 향합니다. 요셉도 잉태한 마리아와 함께 나사렛을 떠나, 다윗의 동네 베들레헴으로 올라갑니다.", "joseph"),
                    Dramatic("요셉", "joseph", "베들레헴으로 가야 하오. 함께 길을 떠납시다.") }; break;
                case "birth-birth-manger": turns = new[] {
                    Narrate("베들레헴에 있을 때, 마리아가 아기를 낳았습니다. 강보로 싸서 구유에 뉘었습니다. 사관에는 머물 곳이 없었습니다.", "mary"),
                    Dramatic("마리아", "mary", "이 작은 아기를 품에 안고, 조심스럽게 강보로 감싸겠어요.") }; break;
                case "birth-night-shepherds": turns = new[] {
                    Narrate("그 지경의 목자들은 밤에도 밖에서 양 떼를 지키고 있었습니다. 주의 사자가 곁에 서고, 영광이 목자들을 두루 비춥니다."),
                    Dramatic("목자", "teacher", "저 빛을 보아! 무슨 일이 일어난 걸까?") }; break;
                case "birth-birth-good-news": turns = new[] {
                    Narrate("크게 두려워하는 목자들에게 천사가 말을 건넵니다."),
                    Speech(r, 10, "천사", "teacher", "천사가 이르되 ") }; break;
                case "birth-manger-witnesses": turns = new[] {
                    Speech(r, 15, "목자들", "teacher", "목자가 서로 말하되 ", " 하고"),
                    Narrate("목자들은 서둘러 마리아와 요셉, 구유에 누인 아기를 찾습니다. 보고 나서는 천사에게 들은 말을 전합니다. 듣는 사람들은 그 말을 기이하게 여겼습니다.", "joseph") }; break;
                case "birth-birth-remembered": turns = new[] {
                    Narrate("마리아는 목자들의 말을 마음에 지키며 생각합니다. 목자들은 들은 그대로 보고, 하나님께 영광을 돌리며 돌아갔습니다.", "mary"),
                    Dramatic("마리아", "mary", "오늘 들은 말들을 오래 마음에 간직하겠어요.") }; break;
                case "birth-name-jesus": turns = new[] {
                    Narrate("팔 일이 되었습니다. 아기는 예수라는 이름을 받습니다. 수태하기 전에 천사가 일컬었던 이름입니다.", "mary"),
                    Dramatic("요셉", "joseph", "이 아기의 이름을 예수라 하겠소.") }; break;
                case "birth-temple-dedication": turns = new[] {
                    Narrate("부모는 모세의 법대로 결례의 날이 차자 아기를 데리고 예루살렘으로 올라갑니다.", "joseph"),
                    Dramatic("요셉", "joseph", "아기를 데리고 성전으로 갑시다. 율법에 기록된 대로 행해야겠소.") }; break;
                case "birth-simeon-peace": turns = new[] {
                    Narrate("성전에서 시므온이 아기를 안습니다. 기다리던 만남 앞에서 그는 하나님을 찬송합니다."),
                    Speech(r, 29, "시므온", "teacher") }; break;
                case "birth-anna-thanksgiving": turns = new[] {
                    Narrate("그 때에 안나도 나아옵니다. 하나님께 감사하고, 예루살렘의 구속을 바라는 사람들에게 이 아기에 관해 말합니다."),
                    Narrate("서로 다른 사람들이 아기를 만났습니다. 가족은 율법을 따라 행할 일을 마치고, 갈릴리 나사렛으로 돌아갑니다.", "joseph") }; break;
                case "birth-nazareth-growth": turns = new[] {
                    Narrate("나사렛의 날들이 흐릅니다. 아기는 자라며 강하여지고 지혜가 충족하며, 하나님의 은혜가 그 위에 있었습니다.", "mary"),
                    Narrate("열두 살이 된 예수님은 부모와 함께 유월절을 지키러 예루살렘으로 올라갑니다.", "joseph") }; break;
                // These short family/wayfinding exchanges stay inside the
                // recorded journey. Their exact wording remains dramatized,
                // never newly attributed Scripture or an invented Jesus saying.
                case "nazareth-departure": turns = new[] {
                    Dramatic("요셉", "joseph", "유월절 길을 떠날 채비가 되었니? 예루살렘까지 함께 가자."),
                    Dramatic("요셉", "joseph", "샘가에 마리아가 기다리고 있단다. 먼저 가서 만나 보렴.") }; break;
                case "pilgrim-spring": turns = new[] {
                    Dramatic("마리아", "mary", "왔구나. 유월절을 지키러 예루살렘으로 올라가자."),
                    Dramatic("마리아", "mary", "이 길을 따라 성전 뜰로 가자. 내 곁으로 오렴.") }; break;
                case "festival-courtyard": turns = new[] {
                    Dramatic("성전 길잡이", "teacher", "유월절을 지키러 오셨군요. 성전 뜰은 이쪽입니다."),
                    Narrate("예수님은 부모와 함께 예루살렘에서 절기를 보냈습니다.", "jesus"),
                    Narrate("절기가 끝난 뒤, 예수님은 예루살렘에 머무셨습니다. 부모는 동행 중에 계신 줄 알고 길을 떠났다가, 예수님을 찾으러 돌아옵니다.", "mary") }; break;
                case "listening-court": turns = new[] {
                    Dramatic("성전의 선생", "teacher", "이야기를 듣고 싶으냐? 안쪽에 선생들이 모여 있단다."),
                    Dramatic("성전의 선생", "teacher", "회랑을 따라 들어오렴. 함께 앉을 자리가 있단다.") }; break;
                case "teachers-circle": turns = new[] {
                    Narrate("예수님은 선생들 가운데 앉아, 듣기도 하시고 묻기도 하셨습니다.", "jesus"),
                    Dramatic("율법 선생", "teacher", "어린 네게서 이런 대답을 듣다니, 놀랍구나."),
                    Narrate("사흘 만에 성전으로 찾아온 부모가 예수님을 보고 놀랍니다. 마리아가 가까이 다가옵니다.", "mary") }; break;
                case "parents-reunite": turns = new[] {
                    Speech(r, 48, "마리아", "mary", "그 모친은 가로되 "),
                    Speech(r, 49, "예수님", "jesus", "예수께서 가라사대 ", " 하시니"),
                    Narrate("양친은 그 하신 말씀을 깨닫지 못했습니다.", "joseph"),
                    Narrate("예수님은 부모와 함께 나사렛으로 내려갑니다. 마리아는 이 모든 말을 마음에 둡니다.", "mary") }; break;
                case "nazareth-return": turns = new[] {
                    Dramatic("요셉", "joseph", "이제 나사렛으로 돌아가자. 내 곁으로 오렴."),
                    Narrate("예수님은 부모에게 순종하여 받드셨습니다. 지혜와 키가 자라가며 하나님과 사람에게 더 사랑스러워 가셨습니다.", "jesus"),
                    Narrate("세월이 흘렀습니다. 장성한 예수님은 갈릴리를 떠나 요단강으로 향하십니다.", "jesus") }; break;
                case "adult-jordan-road": turns = new[] {
                    Narrate("장성한 예수님은 요한에게 세례를 받으려고 갈릴리를 떠나 요단강으로 오십니다.", "jesus"),
                    Dramatic("요단 길잡이", "teacher", "나무 그늘을 지나 강가로 가 보세요. 그곳에 요한이 있습니다.") }; break;
                case "john-calling": turns = new[] {
                    Narrate("하나님의 말씀이 빈 들에서 사가랴의 아들 요한에게 임했습니다."),
                    Narrate("요한은 요단강 부근 각처에 와서 죄 사함을 얻게 하는 회개의 세례를 전합니다. 예수님도 이 강가로 오십니다.", "jesus") }; break;
                case "baptism-request": turns = new[] {
                    Speech(r, 14, "세례 요한", "teacher", "요한이 말려 가로되 "),
                    Speech(r, 15, "예수님", "jesus", "예수께서 대답하여 가라사대 ", " 하신대") }; break;
                case "baptism-water": turns = new[] {
                    Narrate("예수님은 세례를 받으시고 물에서 올라오십니다. 하늘이 열리고, 하나님의 성령이 비둘기 같이 내려 임하심을 보십니다.", "jesus"),
                    Speech(r, 17, "하늘에서 들린 소리", "teacher", "하늘로서 소리가 있어 말씀하시되 ", " 하시니라") }; break;
                case "ministry-beginning": turns = new[] {
                    Narrate("예수님이 가르치심을 시작하셨습니다.", "jesus"),
                    Narrate("그 때 예수님은 삼십세쯤이셨습니다.", "jesus"),
                    Narrate("사람들은 예수님을 요셉의 아들로 알고 있었습니다.") }; break;
                default: throw new ArgumentException("Unknown story dialogue: " + quest.Id);
            }
            if (turns.Length != quest.Dialogue.Count)
                throw new InvalidOperationException("Story beat count would invalidate an existing save: " + quest.Id);
            return Array.AsReadOnly(turns);
        }
    }
}
