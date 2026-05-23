using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Script.Interfaces
{

        public interface IHasHealth
        {
            int GetCurrentHp();
            int GetMaxHp();
            event Action<int, int> OnHpChanged;
        }
    
}
