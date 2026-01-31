using System;
using System.Collections.Generic;
using System.Text;

namespace WpfApp3
{
    public class DatabaseConfig
    {
        public string ConnectionString { get; set; }
        public int MaxRetryAttempts { get; set; }
        public int TimeoutSeconds { get; set; }
    }
}
