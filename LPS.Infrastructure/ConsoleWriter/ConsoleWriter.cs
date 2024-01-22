using LPS.Domain.Domain.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LPS.Infrastructure.ConsoleWriter
{
    public class ConsoleWriter //: IConsoleWriter
    {
        private static List<Message> _messageList = new List<Message>();
        private static readonly object lockObject = new object();

        public int MaxNumberOfMessagesToDisplay { get; set; }
        public int MaxNumberOfMessages { get; set; }

        private static bool _stopPrinting;
        static Task writer;
        public ConsoleWriter(int maxMessagesToDisplay, int maxNumberOfMessages)
        {
            MaxNumberOfMessagesToDisplay = maxMessagesToDisplay;
            MaxNumberOfMessages = maxNumberOfMessages;

            if (writer == null)
            {

                writer = Task.Run(async () =>
                {
                    while (true)
                    {
                        await WriteMessages();
                        Thread.Sleep(10000); // Adjust the interval as needed

                    }
                });

            }
        }

        public void AddMessage(string message, int priority, int groupId, ConsoleColor color)
        {
            lock (lockObject)
            {
                var newMessage = new Message(message, priority, groupId, DateTime.Now, color);

                _messageList.Add(newMessage);

                if (_messageList.Count > MaxNumberOfMessages)
                {
                    _messageList = (List<Message>)Prioritize();
                 //   _messageList.RemoveRange(0, _messageList.Count - MaxNumberOfMessages);
                }
            }
        }

        private IList<Message> Prioritize()
        {
            var prioritizedMessages = _messageList
                .GroupBy(m => m?.GroupId)
                .Select(group =>
                {
                    var topMessage = group
                        .OrderByDescending(m => m?.Priority)
                        .ThenByDescending(m => m?.Timestamp)
                        .FirstOrDefault();
                    return topMessage;
                })
                .Where(m => m != null)
                .ToList();

            return prioritizedMessages;
        }

        private async Task WriteMessages()
        {
            await Task.Run(() =>
            {
                lock (lockObject)
                {
                    ClearConsole();
                    foreach (var message in _messageList)
                    {
                        Console.ForegroundColor = message.Color;
                        Console.WriteLine(message.Content);
                    }
                    Console.ResetColor();
                }
            });
        }

        public void StopWriting()
        {
            _stopPrinting = true;
        }

        public void ClearConsole()
        {
            Console.Clear();

        }

        private class Message
        {
            public string Content { get; }
            public int Priority { get; }
            public int GroupId { get; }
            public DateTime Timestamp { get; }
            public ConsoleColor Color { get; }

            public Message(string content, int priority, int groupId, DateTime timestamp, ConsoleColor color)
            {
                Content = content;
                Priority = priority;
                GroupId = groupId;
                Timestamp = timestamp;
                Color = color;
            }
        }
    }
}
