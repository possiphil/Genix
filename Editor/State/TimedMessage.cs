using UnityEditor;

namespace Genix.Editor.State
{
    public sealed class TimedMessage
    {
        public string Text { get; private set; }
        public MessageType Type { get; private set; }

        private double _visibleUntilTime;

        public bool IsVisible => !string.IsNullOrEmpty(Text);

        public void Show(string text, MessageType type = MessageType.Info, double durationSeconds = 4.0)
        {
            Text = text;
            Type = type;
            _visibleUntilTime = EditorApplication.timeSinceStartup + durationSeconds;
        }

        public void Clear()
        {
            Text = null;
            Type = MessageType.None;
            _visibleUntilTime = 0.0;
        }

        public bool Update()
        {
            if (!IsVisible)
                return false;

            if (EditorApplication.timeSinceStartup < _visibleUntilTime)
                return true;

            Clear();
            return false;
        }
    }
}