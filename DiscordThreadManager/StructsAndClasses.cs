using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiscordThreadManager{
    public struct Thread{
        public string Id;
        public string Name;
        public string LastMessageId; //Can get username and timestamp(its somewhere there trust me =) )
        public bool isActive;
        public bool isLocked;
        public ulong LastMessageTime;
        public Thread(string Id, string Name, string LastMessageId, bool isActive, bool isLocked){
            this.Id = Id;
            this.Name = Name;
            this.LastMessageId = LastMessageId;
            this.isActive = isActive;
            this.isLocked = isLocked;
            this.LastMessageTime = (Convert.ToUInt64(LastMessageId) >> 22); // discord Epoch
        }
    }
}
