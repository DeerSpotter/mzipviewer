using EAAddinFramework.Utilities;
using Microsoft.SqlServer.Server;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management.Instrumentation;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Helpers;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;
using TSF_EA = TSF.UmlToolingFramework.Wrappers.EA;



namespace EADataContract
{
    public abstract class ODCSItem
    {
        public static string profile => "ODCS::";
        public string id { get; set; }
        private YamlNode _node = null;
        public YamlNode node 
        { 
            get
            {
                if (_node == null)
                {
                    this.loadYamlNode();
                }
                return _node; 
            } 
            protected set { _node = value; } 
        }
        protected abstract void loadYamlNode();
        public List<ODCSItem> children { get; set; } = new List<ODCSItem>();
        public List<ODCSRelationship> relationships { get; } = new List<ODCSRelationship>();
        protected ODCSItem() { }
        public ODCSItem(YamlNode node, ODCSItem owner)
        {
            this.node = node;
            this.owner = owner;
            this.id = getStringValue("id");
        }
        public ODCSItem(TSF_EA.Element modelElement)
        {
            this.modelElement = modelElement;
            this.id = modelElement.taggedValues.FirstOrDefault(tv => tv.name == "id")?.tagValue.ToString();
        }
        protected void loadFromModel()
        {
            this.loadDataFromModel();
            this.getChildrenFromModel();
            foreach (var child in this.children)
            {
                child.loadFromModel();
            }
        }
        protected abstract void loadDataFromModel();
        protected abstract void getChildrenFromModel(); 

        protected YamlNode getNodeFromString(string yamlString)
        {
            var input = new StringReader(yamlString);
            var yaml = new YamlStream();
            yaml.Load(input);
            if (yaml.Documents.Count > 0)
            {
                return yaml.Documents[0].RootNode;
            }
            return null;
        }
        protected TSF_EA.Element importToModel(TSF_EA.Element context, int position)
        {
            this.getModelElement(context);
            this.updateModelElement(position);
            this.getRelationships();
            this.children = this.getChildItems();
            int i = 0;
            foreach (var childItem in this.children)
            {
                childItem.importToModel(modelElement, i);
                i++;
            }
            return modelElement;
        }
        protected void importRelationships(int position)
        {
            foreach (var relationship in this.relationships)
            {
                relationship.importToModel(this.modelElement, position);
            }
            //process relationships of child items
            int i = 0;
            foreach (var childItem in this.getChildItems())
            {
                childItem.importRelationships(i);
                i++;
            }
        }

        protected void getRelationships()
        {
            YamlNode relationshipsNode = null;
            if (this.node is YamlMappingNode)
            { 
                var children = ((YamlMappingNode)this.node).Children;
                if (children.TryGetValue("relationships", out relationshipsNode))
                {
                    //check if sequence node
                    if (relationshipsNode is YamlSequenceNode)
                    {
                        var relationshipsSeqNode = relationshipsNode as YamlSequenceNode;
                        if (relationshipsSeqNode == null) return;
                        foreach (var relationshipNode in relationshipsSeqNode.Children.OfType<YamlMappingNode>())
                        {
                            var odcsRelationship = new ODCSRelationship(relationshipNode, this);
                            this.relationships.Add(odcsRelationship);
                        }
                    }
                }
            }
            
        }
        public abstract void getModelElement(TSF_EA.Element context);
        public abstract void updateModelElement(int position);
        public abstract List<ODCSItem> getChildItems();
        
        public ODCSItem owner { get; protected set; }

        public TSF_EA.Element modelElement { get; protected set; } = null;
        protected void addKeyValue( string key, string value)
        {
            this.addKeyValue(key, value, this.node);
        }
        protected void addKeyValue(string key, string value, YamlNode node)
        {
            var mappingNode = node as YamlMappingNode;
            if (mappingNode == null)
            {
                throw new InvalidDataException("Cannot add key value pair to non-mapping node");
            }
            if (!string.IsNullOrEmpty(value) && !string.IsNullOrEmpty(key))
            {
                //check if the value can be parsed as a sequenceNode, and add that if needed
                var valueNode = this.parseYamlNode(value);
                this.addKeyValue(key, mappingNode, valueNode);
            }
        }
        protected void addKeyValue(string key, YamlMappingNode mappingNode, YamlNode valueNode)
        {
            if (valueNode != null && !string.IsNullOrEmpty(key))
            {
                if (mappingNode.Children.ContainsKey(key))
                {
                    mappingNode.Children[key] = valueNode;
                }
                else
                {
                    mappingNode.Add(new YamlScalarNode(key), valueNode);
                }
            }
        }
        protected void addKeyValue(string key, YamlNode valueNode)
        {
            var mappingNode = this.node as YamlMappingNode;
            if (mappingNode == null)
            {
                throw new InvalidDataException("Cannot add key value pair to non-mapping node");
            }
            this.addKeyValue(key, mappingNode, valueNode);
        }
        protected void addKeyValue(string key, bool? value)
        {
            if (value.HasValue)
            {
                addKeyValue(key, value.Value ? "true" : "false");
            }
        }
        protected void addKeyValue(string key, int? value)
        {
            if (value.HasValue)
            {
                addKeyValue(key, value.Value.ToString());
            }
        }
        protected void addKeyValue(string key, decimal? value)
        {
            if (value.HasValue)
            {
                addKeyValue(key, value.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        public string getYamlString()
        {

            return new SerializerBuilder()
                .Build().Serialize(this.node).Trim();
        }
        protected string getStringValue(string key)
        {
            if (this.node is YamlMappingNode mappingNode)
            {
                if (mappingNode.Children.TryGetValue(key, out var valueNode))
                    if (valueNode is YamlScalarNode)
                    {
                        return ((YamlScalarNode)valueNode).Value;
                    }
                    else
                    {
                        //if it's not a scalar node, then simply serialize the node the node to get the yamlText
                        return new SerializerBuilder().Build().Serialize(valueNode).Trim();
                    }
            }
            return null;
        }
        protected string getStringValue(string key, YamlNode node)
        {
            if (node is YamlMappingNode mappingNode)
            {
                if (mappingNode.Children.TryGetValue(key, out var valueNode))
                    if (valueNode is YamlScalarNode)
                    {
                        return ((YamlScalarNode)valueNode).Value;
                    }
                    else
                    {
                        //if it's not a scalar node, then simply serialize the node the node to get the yamlText
                        return new SerializerBuilder().Build().Serialize(valueNode).Trim();
                    }
            }
            return null;
        }
        protected bool? getBooleanValue(string key)
        {
            if (bool.TryParse(getStringValue(key), out var booleanValue))
            {
                return booleanValue;
            }
            else
            {
                return null;
            }
        }
        protected int? getIntValue(string key)
        {
            if (int.TryParse(getStringValue(key), out var intValue))
            {
                return intValue;
            }
            else
            {
                return null;
            }
        }
        protected decimal? getDecimalValue(string key)
        {
            if (decimal.TryParse(getStringValue(key), out var decimalValue))
            {
                return decimalValue;
            }
            else
            {
                return null;
            }
        }
        public override string ToString()
        {
            return this.node?.ToString();
        }
        protected YamlNode parseYamlNode(string contents)
        { 
            if (string.IsNullOrEmpty(contents)) return null;
            var input = new StringReader(contents);
            var yaml = new YamlStream();
            yaml.Load(input);
            YamlNode node = null;
            if (yaml.Documents.Count > 0)
            {
                //get the parsed node
                node = yaml.Documents[0].RootNode;
            }
            else
            {
                //make a new scalar node
                node = new YamlScalarNode(contents);
            }
            return node;
        }

    }
}
