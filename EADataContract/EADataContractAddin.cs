using EAAddinFramework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TSF.UmlToolingFramework.Wrappers.EA;

namespace EADataContract
{
    public class EADataContractAddin : EAAddinFramework.EAAddinBase
    {
        const string menuName = "-&EA Data Contract";
        const string menuImport = "&Import Data Contract";
        const string menuExport = "&Export Data Contract";
        const string menuAbout = "&About EA Data Contract";
        const string outputName = "EA Data contract";
        public EADataContractAddin() : base()
        {
            // Add menu's to the Add-in in EA
            this.menuHeader = menuName;
            this.menuOptions = new string[] {
                                menuImport,
                                menuExport,
                                menuAbout
                              };
        }
        public override void EA_MenuClick(EA.Repository Repository, string MenuLocation, string MenuName, string ItemName)
        {
            switch (ItemName)
            {
                case menuImport:
                    this.import();
                    break;
                case menuExport:
                    this.export();
                    break;
                case menuAbout:
                    //TODO: new AboutWindow().ShowDialog(this.model.mainEAWindow);
                    break;
            }
        }
        public override void EA_GetMenuState(EA.Repository Repository, string MenuLocation, string MenuName, string ItemName, ref bool IsEnabled, ref bool IsChecked)
        {
            var selectedPackage = this.model.selectedTreePackage as Package;
            if ( MenuLocation.Equals("Diagram",StringComparison.InvariantCultureIgnoreCase))
            {
                IsEnabled = false;
                return;
            }
            switch (ItemName)
            {
                case menuImport:
                    IsEnabled = (selectedPackage != null);
                    break;
                case menuExport:
                    IsEnabled = (selectedPackage != null
                                && selectedPackage.hasStereotype(ODCSDataContract.stereotype));
                    break;
                case menuAbout:
                    IsEnabled = true;
                    break;
            }
        }

        private void import()
        {

            var contract = ODCSDataContract.getUserSelectedContract();
            if (contract == null) return;
            var selectedPackage = this.model.selectedTreePackage as Package;
            EAOutputLogger.clearLog(this.model, outputName);
            EAOutputLogger.log(this.model, outputName
                           , $"Starting import of datacontract '{contract.name}' in package '{selectedPackage.name}'"
                           , 0
                          , LogTypeEnum.log);
            this.model.wrappedModel.EnableUIUpdates = false;
            //this.model.wrappedModel.BatchAppend = true;
            contract.importContract(selectedPackage);
            this.model.wrappedModel.EnableUIUpdates = true;
            //reload package to see changes
            selectedPackage.refresh();
            //this.model.wrappedModel.BatchAppend = false;
            EAOutputLogger.log(this.model, outputName
                           , $"Finished import of datacontract '{contract.name}' in package '{selectedPackage.name}'"
                           , 0
                          , LogTypeEnum.log);

        }

        private void export()
        {
            var selectedPackage = this.model.selectedTreePackage as Package;
            if (selectedPackage == null) return;
            var userSelectedContract = ODCSDataContract.getUserSelectedContract(true);
            if (userSelectedContract == null) return;
            var fileName = userSelectedContract.filePath;
            EAOutputLogger.clearLog(this.model, outputName);
            EAOutputLogger.log(this.model, outputName
                           , $"Starting export of package '{selectedPackage.name}' to file '{fileName}'"
                           , 0
                           , LogTypeEnum.log);
            var contract = new ODCSDataContract(selectedPackage);
            contract.saveToFile(fileName);
            EAOutputLogger.log(this.model, outputName
                           , $"Finished export of package '{selectedPackage.name}' to file '{fileName}'"
                           , 0
                           , LogTypeEnum.log);

        }
    }
}
