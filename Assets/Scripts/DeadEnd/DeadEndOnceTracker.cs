using System.Collections.Generic;
using System.Text;

namespace RogueShooter.DeadEnd
{
    /// <summary>每个死路 site id 只触发一次；四型各一次后验收。</summary>
    public sealed class DeadEndOnceTracker
    {
        readonly HashSet<string> _ids = new HashSet<string>();
        readonly HashSet<DeadEndEventType> _types = new HashSet<DeadEndEventType>();
        bool _acceptanceLogged;

        public int FiredSiteCount => _ids.Count;
        public int FiredTypeCount => _types.Count;
        public bool AcceptanceLogged => _acceptanceLogged;

        public bool AllFourTypesOnce
        {
            get
            {
                return _types.Contains(DeadEndEventType.ChestReveal)
                    && _types.Contains(DeadEndEventType.MobWave)
                    && _types.Contains(DeadEndEventType.StaticRoom)
                    && _types.Contains(DeadEndEventType.EmptySoft);
            }
        }

        public bool Fired(string id)
        {
            return !string.IsNullOrEmpty(id) && _ids.Contains(id);
        }

        public bool TypeFired(DeadEndEventType type)
        {
            return _types.Contains(type);
        }

        /// <summary>Returns false if this site already fired or type is unknown.</summary>
        public bool TryMark(string id, DeadEndEventType type)
        {
            if (string.IsNullOrEmpty(id) || !DeadEndEventTypes.IsPlayable(type))
                return false;
            if (!_ids.Add(id))
                return false;
            _types.Add(type);
            return true;
        }

        /// <summary>First time all four types are marked, return the acceptance line; later calls return null.</summary>
        public string ConsumeAcceptanceIfReady()
        {
            if (_acceptanceLogged || !AllFourTypesOnce)
                return null;
            _acceptanceLogged = true;
            return DeadEndEventTypes.AcceptanceLine;
        }

        public void Reset()
        {
            _ids.Clear();
            _types.Clear();
            _acceptanceLogged = false;
        }

        public string HudLine()
        {
            var sb = new StringBuilder();
            sb.Append("DeadEnd ");
            Append(sb, "DE01", DeadEndEventType.ChestReveal);
            sb.Append(' ');
            Append(sb, "DE02", DeadEndEventType.MobWave);
            sb.Append(' ');
            Append(sb, "DE03", DeadEndEventType.EmptySoft);
            sb.Append(' ');
            Append(sb, "DE04", DeadEndEventType.StaticRoom);
            if (AllFourTypesOnce)
                sb.Append("  four-types once");
            return sb.ToString();
        }

        void Append(StringBuilder sb, string id, DeadEndEventType type)
        {
            sb.Append(id);
            sb.Append('=');
            sb.Append(type);
            sb.Append(_ids.Contains(id) ? "✓" : "·");
        }
    }
}
