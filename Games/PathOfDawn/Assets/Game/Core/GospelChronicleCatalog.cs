using System;
using System.Collections.Generic;
using System.Linq;

namespace PathOfDawn.Core
{
    public sealed class GospelChronicleQuest
    {
        public string Id { get; }
        public string ChapterId { get; }
        public string Title { get; }
        public string Objective { get; }
        public string NpcId { get; }
        public string NpcName { get; }
        public string Portrait { get; }
        public string LandmarkId => Id;
        public float X { get; }
        public bool IsPrologue => ChapterId=="birth";
        public GospelReading Reading { get; }
        public string ReadingId => Reading==null?"":Reading.Id;
        public IReadOnlyList<GospelDialogueTurn> Dialogue { get; }
        public GospelChronicleQuest(string id,string chapter,string title,string objective,string npc,string name,string portrait,float x,GospelReading reading,params GospelDialogueTurn[] dialogue)
        { Id=id;ChapterId=chapter;Title=title;Objective=objective;NpcId=npc;NpcName=name;Portrait=portrait;X=x;Reading=reading;Dialogue=Array.AsReadOnly(dialogue); }
    }

    public static class GospelChronicleCatalog
    {
        public const string Revision="gospel-chronicle-2026-10-29";
        public const int FirstAdultQuest=18;
        public const string HistoricalNote="복음서에 기록된 사건을 따라가는 학습 여정입니다. 지형·이동 거리·길 안내 대화는 게임을 위한 극화이며, 기록되지 않은 예수님의 발언을 성경 말씀으로 만들지 않습니다.";
        static GospelDialogueTurn Summary(string text,string portrait="teacher") => new GospelDialogueTurn("복음서의 이야기",portrait,text);
        static GospelDialogueTurn Direction(string name,string portrait,string text) => new GospelDialogueTurn(name,portrait,text,false,true);
        static GospelDialogueTurn Quote(GospelReading reading,string portrait="teacher") => new GospelDialogueTurn("성경의 기록",portrait,reading.Text,true);
        static GospelReading Reading(string id)
        {
            foreach(var reading in GospelChronicleScripture.Readings)if(reading.Id==id)return reading;
            return GospelStudyCatalog.GetReading(id);
        }
        static GospelChronicleQuest Birth(string id,string summary,string portrait="mary")
        {
            var r=Reading(id);
            return new GospelChronicleQuest("birth-"+id,"birth",r.Title,"가족의 여정을 따라가세요.","narrator","복음서의 이야기",portrait,0,r,Summary(summary,portrait),Quote(r,portrait));
        }
        static GospelChronicleQuest World(string id,string chapter,string title,string objective,string npc,string name,string portrait,float x,string reading,params GospelDialogueTurn[] dialogue)
            => new GospelChronicleQuest(id,chapter,title,objective,npc,name,portrait,x,reading==null?null:Reading(reading),dialogue);

        public static IReadOnlyList<GospelChronicleQuest> Quests { get; }=Array.AsReadOnly(new[] {
            Birth("road-bethlehem","호적 명령이 내려졌습니다. 요셉과 마리아는 나사렛에서 베들레헴으로 올라갑니다.","joseph"),
            Birth("birth-manger","베들레헴에 있을 때 해산할 날이 차서 아기가 태어났습니다. 마리아는 아기를 강보로 싸서 구유에 뉘었습니다."),
            Birth("night-shepherds","그 지경에서 밤에 양 떼를 지키던 목자들 곁에 주의 사자가 나타났습니다. 평범하던 밤이 소식을 듣는 밤으로 바뀝니다.","teacher"),
            Birth("birth-good-news","천사는 두려워하는 목자들에게 구주이신 그리스도 주께서 나셨다는 소식을 전합니다.","teacher"),
            Birth("manger-witnesses","목자들은 베들레헴으로 가서 아기를 찾고, 천사에게 들은 말을 전했습니다.","teacher"),
            Birth("birth-remembered","마리아는 들은 말을 마음에 지켜 생각했습니다. 목자들은 하나님께 영광을 돌리고 찬송하며 돌아갔습니다."),
            Birth("name-jesus","팔 일이 되어 아기의 이름을 예수라 하였습니다. 수태하기 전에 천사가 일러 준 이름입니다."),
            Birth("temple-dedication","부모는 모세의 법을 따라 아기를 데리고 예루살렘으로 올라갔습니다.","joseph"),
            Birth("simeon-peace","시므온은 아기를 안고 하나님을 찬송하며 이방을 비추는 빛과 이스라엘의 영광을 노래했습니다.","teacher"),
            Birth("anna-thanksgiving","성전에는 안나도 있었습니다. 안나는 하나님께 감사하며 예루살렘의 구속을 바라는 사람들에게 아기에 관하여 말했습니다.","teacher"),
            Birth("nazareth-growth","나사렛에서 예수님은 자라셨습니다. 열두 살이 된 해에는 부모와 함께 예루살렘으로 올라갑니다.","joseph"),
            World("nazareth-departure","temple","유월절 길을 준비하며","나사렛 길목에서 요셉과 출발 준비를 이야기하세요.","joseph","요셉","joseph",-27,null,
                Summary("부모는 해마다 유월절에 예루살렘으로 갔습니다. 예수님이 열두 살이 되던 해에도 가족은 절기를 지키러 올라갑니다."),
                Direction("요셉","joseph","여행길에서는 함께 걷는 이들을 놓치지 않도록 살펴보자. 준비가 되면 샘가 길목에 있는 마리아에게 가 보렴.")),
            World("pilgrim-spring","temple","함께 오르는 길","샘가 길목으로 걸어가 마리아와 여정의 목적을 이야기하세요.","mary","마리아","mary",-12,null,
                Summary("가족은 유월절을 지키러 예루살렘으로 올라갑니다."),
                Direction("마리아","mary","이 길을 지나면 성전 뜰로 이어진단다. 길을 잘 살피며 성전 길잡이가 있는 곳까지 가 보자.")),
            World("festival-courtyard","temple","예루살렘의 절기","성전 뜰의 길잡이와 이야기하세요.","temple-guide","성전 길잡이","teacher",2,"temple-passover",
                Summary("절기를 마친 뒤, 부모는 예수님이 예루살렘에 머무셨다는 것을 알지 못하고 돌아갑니다."),
                Quote(Reading("temple-passover")),
                Summary("부모는 예수님이 동행 중에 계신 줄로 생각했습니다. 그를 찾지 못하자 예루살렘으로 돌아옵니다.")),
            World("listening-court","temple","듣고 묻는 자리","회랑에서 성전의 선생을 만나 대화하세요.","teacher-listening","성전의 선생","teacher",11,null,
                Direction("성전의 선생","teacher","회랑을 따라 들어오렴. 안쪽에 선생들이 모여 있단다."),
                Summary("예수님은 선생들 가운데 앉아 듣기도 하시고 묻기도 하셨습니다.")),
            World("teachers-circle","temple","선생들 가운데","안쪽 뜰의 율법 선생을 만나 성전의 이야기를 이어가세요.","teacher-reading","율법 선생","teacher",18,"temple-teachers",
                Summary("사흘 후 부모는 성전에서 예수님을 만났습니다. 예수님은 선생들 가운데 앉아 듣기도 하시고 묻기도 하셨습니다."),
                Quote(Reading("temple-teachers")),
                Summary("듣는 사람들은 그 지혜와 대답을 기이하게 여겼습니다. 부모가 예수님께 다가옵니다.")),
            World("parents-reunite","temple","근심 끝의 만남","성전으로 찾아온 부모에게 다가가세요.","mary","마리아","mary",25,"temple-parents",
                GospelConversations.ForReading("temple-parents").Take(3).Concat(new[] { Summary("예수님은 부모와 함께 나사렛으로 내려갑니다. 마리아는 이 모든 말을 마음에 둡니다.", "mary") }).ToArray()),
            World("nazareth-return","temple","나사렛으로 함께","귀향길의 요셉과 함께 나사렛으로 돌아가세요.","joseph","요셉","joseph",34,"temple-nazareth",
                Quote(Reading("temple-nazareth"),"joseph"),
                Summary("예수님은 부모와 함께 나사렛으로 내려가 순종하여 받드셨습니다. 지혜와 키가 자라가며 하나님과 사람에게 더 사랑스러워 가셨습니다."),
                Summary("세월이 흘렀습니다. 장성한 예수님은 갈릴리를 떠나 요단강으로 향하십니다.")),
            World("adult-jordan-road","baptism","요단으로 향하는 길","요단 길목의 안내자를 만나세요.","jordan-guide","요단 길잡이","teacher",-26,null,
                Summary("예수님은 요한에게 세례를 받으려고 갈릴리를 떠나 요단강으로 오십니다."),
                Direction("요단 길잡이","teacher","강가에 세례 요한이 있습니다. 나무 그늘을 지나 강가의 만남으로 걸어가 보세요.")),
            World("john-calling","baptism","요단에서 들리는 외침","강가에서 세례 요한을 만나세요.","john-baptist","세례 요한","teacher",-12,"john-jordan",
                Summary("하나님의 말씀이 빈 들에서 요한에게 임했습니다. 요한은 요단강 부근에서 회개의 세례를 전파합니다."),
                Quote(Reading("john-jordan"))),
            World("baptism-request","baptism","모든 의를 이루는 길","세례 자리에서 요한을 만나 이야기를 이어가세요.","john-baptist","세례 요한","teacher",2,"baptism-request",
                Summary("요한은 자신이 예수님께 세례를 받아야 한다며 예수님을 말립니다."),
                Quote(Reading("baptism-request"),"jesus")),
            World("baptism-water","baptism","물에서 올라오시다","세례를 받으신 뒤의 강가로 가세요.","jordan-witness","강가의 기록 안내자","teacher",19,"baptism-water",
                Summary("예수님은 세례를 받으시고 물에서 올라오십니다. 하늘이 열리고 성령이 비둘기 같이 내려 임하십니다."),
                Quote(Reading("baptism-water"),"jesus")),
            World("ministry-beginning","baptism","공생애의 문턱","강가의 길을 따라 다음 만남으로 가세요.","journey-narrator","여정의 기록자","teacher",34,"ministry-thirty",
                Summary("예수님이 가르치심을 시작하실 때는 삼십세쯤이었습니다."),
                Quote(Reading("ministry-thirty"),"jesus"),
                Summary("사람들은 예수님을 요셉의 아들로 알고 있었습니다."))
        });

        public static IReadOnlyList<GospelReading> Readings { get; }=Quests.Where(q=>q.Reading!=null).Select(q=>q.Reading).ToList().AsReadOnly();
        public static GospelReading GetReading(string id) => Readings.FirstOrDefault(r=>r.Id==id)??throw new ArgumentException("알 수 없는 생애 말씀: "+id);
    }
}
