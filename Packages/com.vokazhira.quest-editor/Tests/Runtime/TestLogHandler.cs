using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vokazhira.QuestEditor.Tests
{
    /// <summary>
    /// Запоминает сообщения логгера и ничего не выводит в Console. Используется как <c>new Logger(handler)</c>.
    /// </summary>
    internal sealed class TestLogHandler : ILogHandler
    {
        public readonly List<KeyValuePair<LogType, string>> Messages = new List<KeyValuePair<LogType, string>>();

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            Messages.Add(new KeyValuePair<LogType, string>(logType, args == null || args.Length == 0 ? format : string.Format(format, args)));
        }

        public void LogException(Exception exception, Object context)
        {
            Messages.Add(new KeyValuePair<LogType, string>(LogType.Exception, exception.ToString()));
        }

        public bool Contains(LogType logType, string fragment)
        {
            return Messages.Exists(m => m.Key == logType && m.Value.Contains(fragment));
        }

        public int Count(LogType logType)
        {
            return Messages.FindAll(m => m.Key == logType).Count;
        }

        public override string ToString()
        {
            return string.Join("\n", Messages.ConvertAll(m => $"[{m.Key}] {m.Value}"));
        }
    }
}
