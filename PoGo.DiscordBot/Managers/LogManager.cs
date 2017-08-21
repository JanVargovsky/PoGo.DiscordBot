using System.Collections.Generic;

namespace PoGo.DiscordBot.Managers
{
    public class LogManager
    {
        readonly LinkedList<string> logs;

        public LogManager()
        {
            logs = new LinkedList<string>();
        }

        public IEnumerable<string> GetLogs(int count, int skip)
        {
            foreach (var log in logs)
            {
                if (--skip >= 0)
                    continue;
                else if (--count >= 0)
                    yield return log;
                else
                    break;
            }
        }

        public void AddLog(string message)
        {
            logs.AddFirst(message);
            while (logs.Count > 100)
                logs.RemoveLast();
        }
    }
}
