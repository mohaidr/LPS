using LPS.Domain;
using LPS.Domain.Common;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LPS.UI.Core
{
    internal class LpsSerializer
    {
        public string Serialize(LPSTest.SetupCommand command)
        {
            try
            {
                string json = JsonConvert.SerializeObject(command);
                return json;
            }
            catch (Exception ex) 
            { 
                throw new InvalidOperationException($"Serialization Has Failed {ex.Message}"); 
            }
        }

        public LPSTest.SetupCommand DeSerialize(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<LPSTest.SetupCommand>(json);

            }
            catch (Exception ex) 
            { 
                throw new InvalidOperationException($"{json} can't be deserialized to an object of type LPSTest.SetupCommand\n{ex.Message}"); 
            }
        }
    }
}
