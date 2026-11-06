using MiniChess.Bot.Rules;

namespace MiniChess.Bot.Eval
{
    /// <summary>
    /// 판 점수의 한 항목. 판 위에 아직 드러나지 않은 가치(점령 진행, 남은 AP, 착탄 대기 포격 등)나 판 자체의 가치를 점수로 바꾼다.
    /// 규칙의 결과(피해, 이동, 턴 효과)는 탐색이 Core 로 실제 실행해 판에 반영하므로, 항목은 "실행해 봐도 아직 안 보이는 것" 만 맡는다.
    /// 새 메커닉은 항목 하나를 추가해 Evaluator.CreateDefault 에 등록한다.
    /// </summary>
    internal interface IEvalTerm
    {
        /// <summary>항목 이름. 가중치 배수(EvalScales)와 점수 분해(Explain)의 키다.</summary>
        string Name { get; }

        /// <summary>a.Team 기준 점수(높을수록 좋다). 단위는 대략 "상대에게 준 HP 피해 1".</summary>
        double Evaluate(BoardAnalysis a);
    }
}
