using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerRole1
{
    class Entity : TableEntity
    {
        public Entity(String key, String value)
        {
            this.PartitionKey = key;
            this.RowKey = value;
        }

        public Entity() { }
    }
}
