using BrightIdeasSoftware;
using EAAddinFramework.Mapping;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MP = MappingFramework;
using TSF_EA = TSF.UmlToolingFramework.Wrappers.EA;
using UML = TSF.UmlToolingFramework.UML;

namespace EAMapping
{
    public partial class CopyMappingForm : Form
    {
        public CopyMappingForm()
        {
            InitializeComponent();
            setDelegates();
        }
        private Mapping mapping { get; set; }
        public void initialize(MP.Mapping mapping)
        {
            this.mapping = (Mapping)mapping;
            this.mappingToTextBox.Text = this.mapping.target.mappingPathExportString;
            this.filterTextBox.Text = $".*{mapping.source.name}";
            this.filterSourceNodes();
        }
        private void setDelegates()
        {
            this.sourceObjectListView.RowFormatter = delegate (OLVListItem listItem) {
                MappingNode node = (MappingNode)listItem.RowObject;
                if (node.isReadOnly)
                {
                    listItem.BackColor = Color.LightGray;
                }
                else
                {
                    listItem.BackColor = Color.White;
                }
            };
            //tell the control which image to show
            ImageGetterDelegate imageGetter = delegate (object rowObject)
            {
                if (rowObject is ElementMappingNode)
                {
                    if (((ElementMappingNode)rowObject).source is UML.Classes.Kernel.Package)
                    {
                        return "packageNode";
                    }
                    else
                    {
                        return "classifierNode";
                    }
                }
                if (rowObject is AttributeMappingNode)
                {
                    return "attributeNode";
                }

                if (rowObject is AssociationMappingNode)
                {
                    return "associationNode";
                }
                else
                {
                    return string.Empty;
                }
            };
            this.sourceFQNColumn.ImageGetter = imageGetter;
        }
        private void filterSourceNodes()
        {
            //get the filtered nodes based on the filter text,
            //but exclude the source node and the nodes that are already mapped to the target node
            var filteredNodes = this.mapping.mappingSet
                                            .getFilteredNodes(this.filterTextBox.Text)
                                            .OfType<MappingNode>()
                                            .Where(x=> x != this.mapping.source
                                                        && ! x.mappings.Any(y => y.target == this.mapping.target));
            foreach (var item in filteredNodes)
            {
                item.isSelected = false;
            }
            this.sourceObjectListView.SetObjects( filteredNodes);

        }
        private void filterButton_Click(object sender, EventArgs e)
        {
            this.filterSourceNodes();
        }

        private void copyButton_Click(object sender, EventArgs e)
        {
            foreach (var node in this.sourceObjectListView.Objects.OfType<MappingNode>().Where(x => x.isSelected))
            {
                var newMapping = node.mapTo(this.mapping.target);
                //copy mapping logics
                foreach (var mappingLogic in this.mapping.mappingLogics)
                {
                    var newMappingLogic = new MappingLogic(mappingLogic.description, mappingLogic.context as TSF_EA.ElementWrapper);
                    newMapping.addMappingLogic(newMappingLogic);
                }
            }
            this.Close();
        }
    }
}
