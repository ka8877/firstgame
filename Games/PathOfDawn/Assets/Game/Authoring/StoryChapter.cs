using System;
using UnityEngine;
using Ink.UnityIntegration;

namespace PathOfDawn.Authoring
{
    [Serializable] public sealed class NarrativeSource
    {
        public string routeId;
        public bool biblicalBaseline;
        [TextArea] public string sourceReferences;
        [TextArea] public string fictionalElements;
    }
    [CreateAssetMenu(menuName = "Path of Dawn/Story Chapter")]
    public sealed class StoryChapter : ScriptableObject
    {
        public string stableId = "galilee_dawn";
        public string contentVersion = "galilee-0.1.0";
        public InkFile story;
        public string[] checkpointIds = { "shore_before_choice", "room_before_choice" };
        public NarrativeSource[] sources = {
            new NarrativeSource { routeId = "witness", biblicalBaseline = true,
                sourceReferences = "마가복음 6:30–44; 요한복음 6:1–13",
                fictionalElements = "여행자, 미리암, 석조 방, 모든 직접 대사는 창작 연결 장면. 오병이어를 목격하기 전 나눔을 준비하는 문턱까지만 다룬다." },
            new NarrativeSource { routeId = "shelter", biblicalBaseline = false,
                sourceReferences = "성경 서술의 대체 결말이 아닌 가상 분기",
                fictionalElements = "여행자가 작은 석조 쉼터에 머무는 창작 사건" },
            new NarrativeSource { routeId = "reconcile", biblicalBaseline = false,
                sourceReferences = "성경 서술의 대체 결말이 아닌 가상 분기",
                fictionalElements = "군중의 사정을 서로 연결하는 창작 사건; 기억으로 열리는 추가 선택" }
        };
    }
}
