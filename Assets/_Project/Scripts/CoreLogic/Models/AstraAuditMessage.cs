using System;

namespace AstraLink.Core
{
    [Serializable]
    public class AstraAuditMessage
    {
        public AuditSeverity Severity;
        public string Title;
        public string Advice;
    }
}
