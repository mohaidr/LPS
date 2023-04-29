using LPS.Domain.Common;

namespace LPS.Domain
{

    public partial class LPSTestCase
    {
   
        public class Validator: IValidator<LPSTestCase, SetupCommand>
        {
            public Validator(LPSTestCase entity , SetupCommand command)
            {
                Validate(entity, command);
            }

            public void Validate(LPSTestCase entity,SetupCommand command)
            {
              
            }
        }
    }
}

