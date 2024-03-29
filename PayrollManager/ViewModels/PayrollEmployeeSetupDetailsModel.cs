﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Data;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Windows.Controls;
using PayrollManager.DataLayer;
using EntityState = System.Data.EntityState;

namespace PayrollManager
{
	public class PayrollEmployeeSetupDetailsModel : BaseViewModel
	{
      

		public PayrollEmployeeSetupDetailsModel()
		{
            staticPropertyChanged += PayrollEmployeeSetupDetailsModel_staticPropertyChanged;
		}

   
        void PayrollEmployeeSetupDetailsModel_staticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            

            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == "CurrentEmployee" || e.PropertyName == "CurrentPayrollJobType")
            {
                GetPayrollEmployeeSetups();
                

            }
        }

   


	    public void UpdateProperties()
        {
            
           
            OnStaticPropertyChanged("PayrollEmployeeSetup");
            OnStaticPropertyChanged("CurrentEmployee");
           

        }


        public ListCollectionView ChargeTypes
        {
            get
            {
                using (var ctx = new PayrollDB())
                {
                    return new ListCollectionView(ctx.ChargeTypes.ToList());
                }
            }
        }

	  


	    public void SavePayrollEmployeeSetup()
	    {
            if (CurrentPayrollEmployeeSetup == null) return;
            using (var ctx = new PayrollDB())
	        {
	            if (CurrentPayrollEmployeeSetup.PayrollEmployeeSetupId == 0)
	            {
	                ctx.PayrollEmployeeSetups.AddObject(CurrentPayrollEmployeeSetup);

	            }
	            else
	            {
	                if (CurrentPayrollEmployeeSetup.EntityState == EntityState.Added) return;
                    var ritm = ctx.PayrollEmployeeSetups.First(
	                    x => x.PayrollEmployeeSetupId == CurrentPayrollEmployeeSetup.PayrollEmployeeSetupId);
	                ctx.PayrollEmployeeSetups.Attach(ritm);
	                ctx.PayrollEmployeeSetups.ApplyCurrentValues(CurrentPayrollEmployeeSetup);
                    
	            }

	            SaveDatabase(ctx);
	            

            }

	        OnStaticPropertyChanged("PayrollEmployeeSetup");

	    }

	    public void DeletePayrollEmployeeSetup(PayrollEmployeeSetup pi)
	    {
            
            if (pi.PayrollEmployeeSetupId != 0)
	        using (var ctx = new PayrollDB())
	        {
	            var ritm = ctx.PayrollEmployeeSetups.First(x => x.PayrollEmployeeSetupId == pi.PayrollEmployeeSetupId);
                ctx.PayrollEmployeeSetups.DeleteObject(ritm);
                SaveDatabase(ctx);
	        }
	        pi = null;
	    }




		#region INotifyPropertyChanged
		public event PropertyChangedEventHandler PropertyChanged;
        

		private void NotifyPropertyChanged(String info)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(info));
			}
		}
		#endregion

        
    }
}