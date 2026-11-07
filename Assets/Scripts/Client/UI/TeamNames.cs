using MiniChess.Client.Bootstrap;
using MiniChess.Core.Common;

namespace MiniChess.Client.UI
{
    /// <summary>화면에 보일 팀 이름. 이 화면에서 한 팀만 조작하면(봇 상대, 온라인) "내"/"상대", 아니면(한 화면 교대, 봇끼리) "PLAYER n".</summary>
    public static class TeamNames
    {
        public static string Name(Team team) => team == Team.Player1 ? "PLAYER 1" : "PLAYER 2";

        /// <summary>"내"/"상대" 또는 "PLAYER n"("내 턴", "상대 배치" 처럼 뒤에 말을 붙여 쓴다).</summary>
        public static string Whose(GameSession session, Team team)
        {
            Team other = team == Team.Player1 ? Team.Player2 : Team.Player1;
            bool oneSide = session.IsLocallyControlled(team) != session.IsLocallyControlled(other);
            if (!oneSide)
                return Name(team);

            return session.IsLocallyControlled(team) ? "내" : "상대";
        }

        /// <summary>이 화면이 조작하는 팀(하단 AP 게이지에 보일 팀). 한 팀만 조작하면 그 팀, 아니면(한 화면 교대, 봇끼리) 지금 턴인 팀.</summary>
        public static Team Viewing(GameSession session, Team currentTeam)
        {
            bool p1 = session.IsLocallyControlled(Team.Player1);
            bool p2 = session.IsLocallyControlled(Team.Player2);
            if (p1 != p2)
                return p1 ? Team.Player1 : Team.Player2;

            return currentTeam;
        }
    }
}
