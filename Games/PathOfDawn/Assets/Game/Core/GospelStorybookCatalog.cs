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

    /// <summary>
    /// Narrative presentation of the same twenty-three saved events. Every beat
    /// retains its predecessor's index, so an old mid-conversation save remains
    /// valid. The exact full passages remain in the discovered book only.
    /// </summary>
    public static class GospelStorybookCatalog
    {
        public const string Revision = "gospel-storybook-2026-11-02";
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
                case "birth-name-jesus": return Scene("예수라는 이름", "아기 때의 기록", "팔 일이 되었을 때", "birth-manger", "birth", "그 이름을 예수라 하였습니다.");
                case "birth-temple-dedication": return Scene("아기를 안고 성전으로", "예루살렘 성전", "아기 때의 성전 방문", "birth-temple", "birth", "부모는 모세의 법을 따라 아기를 데리고 올라갑니다.");
                case "birth-simeon-peace": return Scene("기다리던 만남", "예루살렘 성전", "시므온의 찬송", "birth-temple", "birth", "시므온은 아기를 안고 하나님을 찬송합니다.");
                case "birth-anna-thanksgiving": return Scene("안나의 감사", "예루살렘 성전", "그 때에 나아와", "birth-temple", "birth", "안나는 감사하며 아기에 관해 말합니다.");
                case "birth-nazareth-growth": return Scene("나사렛의 날들", "갈릴리 나사렛", "성장하시는 예수님", "nazareth", "birth", "시간이 흐릅니다. 다음 장의 예수님은 열두 살입니다.");
                case "nazareth-departure": return Scene("유월절 길을 준비하며", "나사렛 길목", "열두 살", "nazareth", "temple", "요셉과 함께 출발할 준비를 합니다.");
                case "pilgrim-spring": return Scene("함께 오르는 길", "예루살렘으로 향하는 길", "유월절의 여정", "nazareth", "temple", "샘가에서 마리아를 만납니다.");
                case "festival-courtyard": return Scene("절기의 예루살렘", "예루살렘 성전 뜰", "유월절", "temple", "temple", "성전을 오가는 사람들 사이로 길이 이어집니다.");
                case "listening-court": return Scene("듣고 묻는 자리", "성전 회랑", "절기가 지난 뒤", "temple", "temple", "회랑을 따라 선생들이 있는 곳으로 향합니다.");
                case "teachers-circle": return Scene("선생들 가운데", "성전 안쪽 뜰", "부모가 찾기 시작한 지 사흘 후", "temple", "temple", "예수님은 선생들 가운데 앉아 계십니다.");
                case "parents-reunite": return Scene("다시 만난 부모", "예루살렘 성전", "사흘 만의 만남", "temple", "temple", "놀란 마리아가 예수님께 말합니다.");
                case "nazareth-return": return Scene("함께 나사렛으로", "나사렛으로 돌아가는 길", "성전 방문 뒤", "nazareth", "temple", "가족은 함께 내려갑니다. 소년의 날들이 이어집니다.");
                case "adult-jordan-road": return Scene("요단으로 향하다", "요단강으로 향하는 길", "공생애가 시작되는 무렵", "jordan", "baptism", "이제 성인이 된 예수님의 발걸음을 따라갑니다.");
                case "john-calling": return Scene("빈 들에서 들리는 소리", "요단 부근", "요한의 사역", "jordan", "baptism", "요한은 사람들에게 회개의 세례를 전합니다.");
                case "baptism-request": return Scene("모든 의를 이루는 길", "요단강", "요한과의 만남", "jordan", "baptism", "예수님은 요한에게 세례를 받으려 하십니다.");
                case "baptism-water": return Scene("물에서 올라오시다", "요단강", "세례를 받으신 뒤", "jordan", "baptism", "예수님은 물에서 올라오십니다. 하늘에서 소리가 들립니다.");
                case "ministry-beginning": return Scene("공생애의 문턱", "요단의 여정을 마치며", "삼십세쯤", "jordan", "baptism", "탄생부터 이어진 이야기는 가르치심을 시작하는 때에 이릅니다.");
                default: throw new ArgumentException("Unknown story scene: " + quest.Id);
            }
        }

        static GospelStorybookScene Scene(string title, string location, string time, string art, string music, string cue)
            => new GospelStorybookScene(title, location, time, art, music, cue);

        public static GospelDialogueTurn GetBeat(GospelChronicle story)
        {
            if (story == null) throw new ArgumentNullException(nameof(story));
            if (story.IsComplete || story.Stage != GospelQuestStage.Reading) return null;
            return GetBeats(story.CurrentQuest)[story.DialogueIndex];
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
                    Narrate("구유에 뉘었던 아기와 앞으로 자라실 예수님은 같은 분입니다. 이 이름과 함께 한 생애의 이야기가 이어집니다.", "joseph") }; break;
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
                    Narrate("누가는 그 사이의 모든 날을 적지 않았습니다. 이야기는 기록이 남아 있는 다음 만남으로 이어집니다. 이제 여러분이 움직이는 예수님은 열두 살입니다.", "joseph") }; break;
                case "nazareth-departure": turns = new[] {
                    Dramatic("요셉", "joseph", "예루살렘으로 갈 준비가 되었니? 함께 걷는 이들을 살피며 출발하자."),
                    Dramatic("요셉", "joseph", "샘가 길목에 마리아가 있단다. 먼저 가서 만나 보렴.") }; break;
                case "pilgrim-spring": turns = new[] {
                    Dramatic("마리아", "mary", "우리는 유월절을 지키러 예루살렘으로 올라간단다. 길을 잘 살피며 함께 가자."),
                    Narrate("누가는 부모가 해마다 유월절에 예루살렘으로 갔다고 기록합니다. 길과 샘가의 배치는 이 여정을 걸어 보기 위한 표현입니다.", "mary") }; break;
                case "festival-courtyard": turns = new[] {
                    Dramatic("성전 길잡이", "teacher", "성전 뜰에 오셨군요. 유월절을 맞아 예루살렘으로 올라오는 이들이 있습니다."),
                    Narrate("예수님이 열두 살이 되던 해에도 가족은 절기의 전례를 따라 올라왔습니다. 이곳에서 그 날들을 보냈습니다.", "jesus"),
                    Narrate("절기가 끝났습니다. 부모는 동행 중에 계신 줄 알고 길을 떠났지만, 예수님은 예루살렘에 머무셨습니다. 찾지 못한 부모는 예루살렘으로 돌아옵니다.", "mary") }; break;
                case "listening-court": turns = new[] {
                    Dramatic("성전의 선생", "teacher", "안쪽에 선생들이 모여 있습니다. 회랑을 따라 들어오세요."),
                    Narrate("성경에는 이 자리에서 오간 질문의 내용이 남아 있지 않습니다. 그 말을 새로 만들어 예수님의 발언으로 대신하지 않습니다. 기록된 만남을 따라 안쪽 뜰로 갑니다.") }; break;
                case "teachers-circle": turns = new[] {
                    Narrate("사흘 후, 부모는 성전에서 예수님을 찾았습니다. 예수님은 선생들 가운데 앉아, 듣기도 하시고 묻기도 하셨습니다.", "jesus"),
                    Narrate("듣는 사람들은 그 지혜와 대답을 기이하게 여겼습니다. 누가가 남긴 것은 이 만남과 사람들의 반응입니다."),
                    Narrate("이제 부모가 가까이 옵니다. 마리아는 근심하며 아들을 찾았던 마음을 말합니다.", "mary") }; break;
                case "parents-reunite": turns = new[] {
                    Speech(r, 48, "마리아", "mary", "그 모친은 가로되 "),
                    Speech(r, 49, "예수님", "jesus", "예수께서 가라사대 ", " 하시니"),
                    Narrate("양친은 그 하신 말씀을 깨닫지 못했습니다.", "joseph"),
                    Narrate("예수님은 부모와 함께 나사렛으로 내려갑니다. 마리아는 이 모든 말을 마음에 둡니다.", "mary") }; break;
                case "nazareth-return": turns = new[] {
                    Dramatic("요셉", "joseph", "함께 나사렛으로 돌아가자. 집으로 내려갈 때에도 서로 곁을 살피자꾸나."),
                    Narrate("예수님은 부모에게 순종하여 받드셨습니다. 지혜와 키가 자라가며 하나님과 사람에게 더 사랑스러워 가셨습니다.", "jesus"),
                    Narrate("기록은 모든 해를 자세히 잇지 않습니다. 다음 이야기는 성인이 된 예수님이 요단강으로 향하는 때로 이어집니다.") }; break;
                case "adult-jordan-road": turns = new[] {
                    Narrate("이제 성인이 된 예수님입니다. 마태는 예수님이 갈릴리에서 요단강으로 와서 요한에게 세례를 받으려 하셨다고 전합니다.", "jesus"),
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
                    Narrate("탄생의 밤에서 나사렛의 성장, 열두 살의 성전과 요단강의 만남으로 이야기가 이어졌습니다.", "jesus"),
                    Narrate("누가는 예수님이 가르치심을 시작할 때 삼십세쯤 되셨다고 기록합니다. 이야기는 이제 공생애의 문턱에 이릅니다.", "jesus"),
                    Narrate("이번 장은 여기서 마칩니다. 앞으로의 사역과 십자가, 부활과 승천은 다음 이야기입니다. 여러분이 지나온 사건의 말씀은 성경책에 남습니다.") }; break;
                default: throw new ArgumentException("Unknown story dialogue: " + quest.Id);
            }
            if (turns.Length != quest.Dialogue.Count)
                throw new InvalidOperationException("Story beat count would invalidate an existing save: " + quest.Id);
            return Array.AsReadOnly(turns);
        }
    }
}
