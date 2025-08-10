using LPS.Domain.Domain.Common.Enums;


namespace LPS.DTOs
{
    //TODO Create Domain entities for those so we can store them in DB once implemented.
    public class VariableDto : IDto<VariableDto>
    {
        public VariableDto()
        {
            Regex = string.Empty;
            Name = string.Empty;
            Value = string.Empty;
        }

        // Variable name
        public string Name { get; set; }

        // Variable value
        public string Value { get; set; }

        // Type information
        private string? _as;
        public string As
        {
            get => _as?? VariableType.String.ToString();
            set {
                _as =  value;
            
            }
        }

        // Regex pattern for validation
        public string Regex { get; set; }

        // Deep copy method
        public void DeepCopy(out VariableDto targetDto)
        {
            targetDto = new VariableDto
            {
                Name = this.Name, // Copy Name
                Value = this.Value, // Copy Value
                As = this.As, // Copy As
                Regex = this.Regex // Copy Regex
            };
        }
    }
}
