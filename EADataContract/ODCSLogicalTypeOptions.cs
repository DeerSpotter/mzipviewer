using EAAddinFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSF.UmlToolingFramework.Wrappers.EA;
using YamlDotNet.RepresentationModel;
using TSF_EA = TSF.UmlToolingFramework.Wrappers.EA;


namespace EADataContract
{
    public class ODCSLogicalTypeOptions: ODCSItem
    {
        public ODCSLogicalTypeOptions() { }
        protected YamlMappingNode mappingNode => (YamlMappingNode)node;
        internal TSF_EA.Attribute modelAttribute => this.modelElement as TSF_EA.Attribute;
        public ODCSLogicalTypeOptions(TSF_EA.Attribute modelAttribute) : base(modelAttribute)
        {
        }
        public ODCSLogicalTypeOptions(YamlMappingNode node, ODCSProperty owner):base(node, owner)
        {
            this.maxItems = getIntValue("maxItems");
            this.minItems = getIntValue("minItems");
            this.uniqueItems = getBooleanValue("uniqueItems");
            this.format = getStringValue("format");
            this.maximum = getStringValue("maximum");
            this.minimum = getStringValue("minimum");
            this.exclusiveMaximum = getStringValue("exclusiveMaximum");
            this.exclusiveMinimum = getStringValue("exclusiveMinimum");
            this.multipleOf = getDecimalValue("multipleOf");
            this.maxLength = getIntValue("maxLength");
            this.minLength = getIntValue("minLength");
            this.pattern = getStringValue("pattern");
            this.maxProperties = getIntValue("maxProperties");
            this.minProperties = getIntValue("minProperties");
            this.required = getBooleanValue("required");
        }
        public int? maxItems { get; set; }
        public int? minItems { get; set; }
        public bool? uniqueItems { get; set; }
        public string format { get; set; }
        public string exclusiveMaximum { get; set; }
        public string exclusiveMinimum { get; set; }
        public string maximum { get; set; }
        public string minimum { get; set; }
        public decimal? multipleOf { get; set; }
        public int? maxLength { get; set; }
        public int? minLength { get; set; }
        public string pattern { get; set; }
        public int? maxProperties { get; set; } 
        public int? minProperties { get; set; }
        public bool? required { get; set; }

        static Dictionary<string, string> defaultOptions = new Dictionary<string, string>()
            {
                { "maxItems", "-1" },
                { "minItems", "-1" },
                { "uniqueItems", "False" },
                { "format", "" },
                { "maximum", "" },
                { "minimum", "" },
                { "exclusiveMaximum", "" },
                { "exclusiveMinimum", "" },
                { "multipleOf", "-1" },
                { "maxLength", "-1" },
                { "minLength", "-1" },
                { "pattern", "" },
                { "maxProperties", "-1" },
                { "minProperties", "-1" },
                { "required", "False" }

            };

        public override List<ODCSItem> getChildItems()
        {
            return new List<ODCSItem>();//empty list
        }

        public override void getModelElement(Element context)
        {
            this.modelElement = context as TSF_EA.Attribute;
        }

        public override void updateModelElement(int position)
        {
            EAOutputLogger.log($"Updating logical type options for attribute: {this.modelAttribute?.name}"
              , 0
              , LogTypeEnum.log);

            this.modelAttribute.addTaggedValue("maxItems", (this.maxItems ?? -1).ToString());
            this.modelAttribute.addTaggedValue("minItems", (this.minItems ?? -1).ToString());
            this.modelAttribute.addTaggedValue("uniqueItems", this.uniqueItems?.ToString());
            this.modelAttribute.addTaggedValue("format", this.format);
            this.modelAttribute.addTaggedValue("maximum", this.maximum);
            this.modelAttribute.addTaggedValue("minimum", this.minimum);
            this.modelAttribute.addTaggedValue("exclusiveMaximum", this.exclusiveMaximum);
            this.modelAttribute.addTaggedValue("exclusiveMinimum", this.exclusiveMinimum);
            this.modelAttribute.addTaggedValue("multipleOf", (this.multipleOf ?? -1).ToString());
            this.modelAttribute.addTaggedValue("maxLength", (this.maxLength ?? -1).ToString());
            this.modelAttribute.addTaggedValue("minLength", (this.minLength ?? -1).ToString());
            this.modelAttribute.addTaggedValue("pattern", this.pattern);
            this.modelAttribute.addTaggedValue("maxProperties", (this.maxProperties ?? -1).ToString());
            this.modelAttribute.addTaggedValue("minProperties", (this.minProperties ?? -1).ToString());
            this.modelAttribute.addTaggedValue("required", this.required?.ToString());

            this.modelAttribute.save();
        }
        public static bool hasLogicalTypeOptionsInModel(TSF_EA.Attribute modelAttribute)
        {
            
            foreach (var optionName in defaultOptions.Keys)
            {
                var taggedValue = modelAttribute.getTaggedValue(optionName);
                if (taggedValue != null
                    && !string.IsNullOrEmpty(taggedValue.eaStringValue)
                    && !taggedValue.eaStringValue.Equals(defaultOptions[optionName], StringComparison.InvariantCultureIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        protected override void loadDataFromModel()
        {
            this.maxItems = this.getIntegerPropertyValue("maxItems");
            this.minItems = this.getIntegerPropertyValue("minItems");
            this.uniqueItems = this.getBooleanPropertyValue("uniqueItems");
            this.format = this.getStringPropertyValue("format");
            this.maximum = this.getStringPropertyValue("maximum");
            this.minimum = this.getStringPropertyValue("minimum");
            this.exclusiveMaximum = this.getStringPropertyValue("exclusiveMaximum");
            this.exclusiveMinimum = this.getStringPropertyValue("exclusiveMinimum");
            this.multipleOf = this.getDecimalValue("multipleOf");
            this.maxLength = this.getIntegerPropertyValue("maxLength");
            this.minLength = this.getIntegerPropertyValue("minLength");
            this.pattern = this.getStringPropertyValue("pattern");
            this.maxProperties = this.getIntegerPropertyValue("maxProperties");
            this.minProperties = this.getIntegerPropertyValue("minProperties");
            this.required = this.getBooleanPropertyValue("required");
        }

        private decimal? getDecimalPropertyValue(string propertyName)
        {
            var value = this.modelAttribute.getTaggedValue(propertyName)?.decimalValue;
            if (value.HasValue
                && defaultOptions.TryGetValue(propertyName, out var defaultValue))
            {
                if (value.Value.ToString().Equals(defaultValue
                    , StringComparison.InvariantCultureIgnoreCase))
                {
                    value = null;
                }
            }
            return value;
        }

        private bool? getBooleanPropertyValue(string propertyName)
        {
            var value = this.modelAttribute.getTaggedValue(propertyName)?.booleanValue;
            if (value.HasValue
                && defaultOptions.TryGetValue(propertyName, out var defaultValue))
            {
                if (value.Value.ToString().Equals(defaultValue
                    , StringComparison.InvariantCultureIgnoreCase))
                {
                    value = null;
                }
            }
            return value;
        }
        private string getStringPropertyValue(string propertyName)
        {
            var value = this.modelAttribute.getTaggedValue(propertyName)?.stringValue;
            if (! string.IsNullOrEmpty(value)
                && defaultOptions.TryGetValue(propertyName, out var defaultValue))
            {
                if (value.Equals(defaultValue, StringComparison.InvariantCultureIgnoreCase))
                {
                    value = null;
                }
            }
            return value;
        }


        private int? getIntegerPropertyValue(string propertyName)
        {
            var value = this.modelAttribute.getTaggedValue(propertyName)?.integerValue;
            if (value.HasValue
                && defaultOptions.TryGetValue(propertyName, out var defaultValue))
            {
                if (value.Value.ToString().Equals(defaultValue
                    , StringComparison.InvariantCultureIgnoreCase))
                {
                    value = null;
                }
            }
            return value;
        }

        protected override void getChildrenFromModel()
        {
            //do nothing, no children to get
        }

        protected override void loadYamlNode()
        {
            this.node = new YamlMappingNode();
            this.addKeyValue("maxItems", this.maxItems);
            this.addKeyValue("minItems", this.minItems);
            this.addKeyValue("uniqueItems", this.uniqueItems);
            this.addKeyValue("format", this.format);
            this.addKeyValue("maximum", this.maximum);
            this.addKeyValue("minimum", this.minimum);
            this.addKeyValue("exclusiveMaximum", this.exclusiveMaximum);
            this.addKeyValue("exclusiveMinimum", this.exclusiveMinimum);
            this.addKeyValue("multipleOf", this.multipleOf);
            this.addKeyValue("maxLength", this.maxLength);
            this.addKeyValue("minLength", this.minLength);
            this.addKeyValue("pattern", this.pattern);
            this.addKeyValue("maxProperties", this.maxProperties);
            this.addKeyValue("minProperties", this.minProperties);
            this.addKeyValue("required", this.required);
        }
    }
}
