﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace PayrollManager.Properties {
    
    
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.VisualStudio.Editors.SettingsDesigner.SettingsSingleFileGenerator", "14.0.0.0")]
    public sealed partial class Settings : global::System.Configuration.ApplicationSettingsBase {
        
        private static Settings defaultInstance = ((Settings)(global::System.Configuration.ApplicationSettingsBase.Synchronized(new Settings())));
        
        public static Settings Default {
            get {
                return defaultInstance;
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.DefaultSettingValueAttribute("Shula@CCCU")]
        public string MachineName {
            get {
                return ((string)(this["MachineName"]));
            }
        }
        
        [global::System.Configuration.ApplicationScopedSettingAttribute()]
        [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
        [global::System.Configuration.SpecialSettingAttribute(global::System.Configuration.SpecialSetting.ConnectionString)]
        [global::System.Configuration.DefaultSettingValueAttribute(@"metadata=res://*/DataLayer.PayrollDB.csdl|res://*/DataLayer.PayrollDB.ssdl|res://*/DataLayer.PayrollDB.msl;provider=System.Data.SqlClient;provider connection string='Data Source=.\SQLEXPRESS2016;AttachDbFilename=C:\Prism\GitRepositiory\payroll\PayRoll\PayrollManager\PayrollDB.mdf;Integrated Security=True;Connect Timeout=30;MultipleActiveResultSets=True;App=EntityFramework';")]
        public string PayrollDB {
            get {
                return ((string)(this["PayrollDB"]));
            }
        }
    }
}
