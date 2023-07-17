using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestCase
    {
   
        public class Validator: IDomainValidator<LPSTestCase, SetupCommand>
        {
            ILPSLogger _logger;
            public Validator(LPSTestCase entity, SetupCommand command, ILPSLogger logger)
            {
                Validate(entity, command);
                _logger = logger;

            }

            public void Validate(LPSTestCase entity,SetupCommand command)
            {
              
            }
        }
    }
}

