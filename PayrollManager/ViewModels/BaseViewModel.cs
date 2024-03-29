﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Data.Entity;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using PayrollManager.DataLayer;
using System.Data.Objects;
using EmailLogger;
using System.Data;
using System.Data.Objects.DataClasses;
using EntityState = System.Data.EntityState;

namespace PayrollManager
{
    public class BaseViewModel :  INotifyPropertyChanged
    {
        private static BaseViewModel _instance = null;
        public static BaseViewModel Instance => _instance;

        //private static object syncRootbase = new Object();
        //[MyExceptionHandlerAspect]
        public BaseViewModel()
        {
            CurrentYear = DateTime.Now.Year;

            staticPropertyChanged += BaseViewModel_staticPropertyChanged;
            _instance = this;

            LoadInstitutions();
            LoadInstitutionsAndCompanies();
            LoadPayrollSetupItems();
            _generatePayrollItems = new GeneratePayrollItems(this);
        }

        public void LoadInstitutions()
        {
            //using (var ctx = new PayrollDB())
            using (var ctx = new PayrollDB())
            {
                _institutions =  new ObservableCollection<DataLayer.Institution>(ctx.Institutions.Include(x => x.Accounts).Where(x => x.Company == null).ToList());
            }
            
            OnStaticPropertyChanged("Institutions");

            
        }

        public void LoadInstitutionsAndCompanies()
        {
            //using (var ctx = new PayrollDB())
            using (var ctx = new PayrollDB())
            {
                _institutionsAndCompanies = new ObservableCollection<DataLayer.Institution>(ctx.Institutions.Include(x => x.Accounts).ToList());
            }

            OnStaticPropertyChanged("InstitutionsAndCompanies");


        }

        public void LoadPayrollSetupItems()
        {
            using (var ctx = new PayrollDB())
            {
                _payrollSetupItems = new ObservableCollection<DataLayer.PayrollSetupItem>(ctx
                    .PayrollSetupItems.ToList());
            }
            OnStaticPropertyChanged("PayrollSetupItems");
        }


        static int instcount = 9999999;
        //[MyExceptionHandlerAspect]
        private void CreateInstitutionEmployeeAccounts()
        {
            try
            {
                if (CurrentPayrollJob == null ) return;

             
                
                //if (db.Accounts.Where(a => a.AccountType == "Summary Account").Count() == 0)
                //{
                using (var ctx = new PayrollDB())
                {
                    foreach (var ist in ctx.EmployeeAccounts.Select(ea => ea.Account.Institution).Distinct())
                    {
                        instcount += 1;
                        // create new institution account

                        var ia =
                            (Account)
                                ist.Accounts.FirstOrDefault(i => i.Institution.Name == ist.Name);
                            //&& i.AccountType == "Summary Account"

                        if (ia == null)
                        {

                            ia = new Account();

                            ia.Institution = ist;
                            ia.AccountName = ist.Name;
                            ia.AccountNumber = "";
                            ia.AccountTypeId = ctx.AccountTypes.First(x => x.Name == "Bank").AccountTypeId;
                            ia.AccountId = instcount; //new Random().Next();
                            HybridAccountsLst.Add(ia);
                        }

                        MergeEmployeAccountIntoAccount(ia);


                    }
                } // db.AcceptAllChanges();
                //}
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public void SavePayrollJob()
        {
            using (var ctx = new PayrollDB())
            {
                if (CurrentPayrollJob == null) return;
                if (CurrentPayrollJob.PayrollJobId == 0)
                {
                    ctx.PayrollJobs.AddObject(CurrentPayrollJob);
                }
                else
                {
                    if (CurrentPayrollJob.EntityState == EntityState.Added) return;
                    var ritm = ctx.PayrollJobs.First(x => x.PayrollJobId == CurrentPayrollJob.PayrollJobId);
                    ctx.PayrollJobs.Attach(ritm);
                    if(CurrentPayrollJob.PreparedBy == null) CurrentPayrollJob.PreparedBy = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
                    ctx.PayrollJobs.ApplyCurrentValues(CurrentPayrollJob);
                }

                SaveDatabase(ctx);
            }
            UpdatePayrollJobs();
            OnStaticPropertyChanged("CurrentPayrollJob");
            OnStaticPropertyChanged("PayrollJobs");

        }

        internal void MergeEmployeAccountIntoAccount(Account ia)
        {
            try
            {
                //var oldmergeopt = db.Accounts.MergeOption;
                //db.Accounts.MergeOption = MergeOption.NoTracking;
                using (var ctx = new PayrollDB())
                {


                    var alst = ctx.EmployeeAccounts.Include(x => x.Account.Institution).Include(x => x.Account.AccountEntries)
                        .Where(i => i.Account.AccountType.Name == "Salary" && i.AccountId == ia.AccountId && i.Account.InstitutionId == ia.InstitutionId).ToList();//ia.Institution

                    foreach (var acc in alst)
                        //Where(i => i.AccountType != "Summary Account"))
                    {


                        instcount += 1;
                        if (acc.Account.CurrentAccountEntries == null ||
                            (acc.Account.CurrentAccountEntries != null && !acc.Account.CurrentAccountEntries.Any()))
                            continue;

                        AccountEntry nae =
                            ia.AccountEntries.FirstOrDefault(
                                x =>
                                {
                                    var firstOrDefault = acc.Account.CurrentAccountEntries.FirstOrDefault();
                                    return firstOrDefault != null && (x.PayrollItem == firstOrDefault.PayrollItem
                                                                      && x.Memo == "Net Entry");
                                });
                        AccountEntry rae =
                            acc.Account.AccountEntries.FirstOrDefault(
                                x =>
                                {
                                    var accountEntry = acc.Account.CurrentAccountEntries.FirstOrDefault();
                                    return accountEntry != null && x.PayrollItem == accountEntry.PayrollItem;
                                });
                        if (rae == null) continue;
                        if (nae == null)
                        {
                            nae = new AccountEntry();
                            ia.AccountEntries.Add(nae);
                            nae.AccountEntryId = instcount;
                            var firstOrDefault = acc.Account.CurrentAccountEntries.FirstOrDefault();
                            if (firstOrDefault != null)
                                nae.PayrollItem = firstOrDefault.PayrollItem;
                            nae.Memo = "Net Entry";
                        }


                        if (acc.Account.Total < 0)
                        {
                            nae.DebitAmount = rae.Total; //acc.Total;

                        }
                        else
                        {
                            nae.CreditAmount = rae.Total; ///acc.Total;

                        }



                        nae.TradeDate = DateTime.Now;

                        //db.ObjectStateManager.ChangeObjectState(nae, System.Data.EntityState.Unchanged);



                    }
                }
                //db.ObjectStateManager.ChangeObjectState(ia, System.Data.EntityState.Unchanged);
                //db.AcceptAllChanges();
                //db.Accounts.MergeOption = oldmergeopt;
            }
            catch (Exception)
            {

                throw;
            }
        }


        public static SliderPanel slider { get; set; }
        ////[MyExceptionHandlerAspect]
        //private void PayrollEmployeeSetup_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        //{
        //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        //    {
        //        db.PayrollEmployeeSetup.AddObject(e.NewItems[0] as DataLayer.PayrollEmployeeSetup);
        //    }
        //}
        ////[MyExceptionHandlerAspect]
        //void PayrollSetupItems_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        //{
        //    if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
        //    {
        //        db.PayrollSetupItems.AddObject(e.NewItems[0] as DataLayer.PayrollSetupItem);
        //        OnStaticPropertyChanged("PayrollSetupItemsRowAdded");
        //    }
        //}


        //[MyExceptionHandlerAspect]
        void AssociationChanged(object sender, CollectionChangeEventArgs e)
        {
            if (sender.ToString().Contains("Employee"))
            {
                OnStaticPropertyChanged("CurrentEmployee");
            }
            if (sender.ToString().Contains("Account"))
            {
                OnStaticPropertyChanged("CurrentAccount");
            }

            //            
        }
        //[MyExceptionHandlerAspect]
        private void BaseViewModel_staticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
             OnPropertyChanged(e.PropertyName);
           
        }


        static private List<AccountType> _accountTypes;
        //[MyExceptionHandlerAspect]
        public List<AccountType> AccountTypes
        {
            get
            {
                if (_accountTypes != null) return _accountTypes;
                using (var ctx = new PayrollDB())
                {
                    _accountTypes = ctx.AccountTypes.ToList();
                }
                return _accountTypes;

            }
        }


        private static ObservableCollection<Company> _companies = new ObservableCollection<Company>(BaseViewModel.LoadCompanies());

        public ObservableCollection<Company> Companies
        {
            get { return _companies; }
            set
            {
                _companies = value; 
                OnPropertyChanged("Companies");
                
            }
        }


        internal static List<Company> LoadCompanies()
        {
            using (var ctx = new PayrollDB())
            {
                var res = ctx.Companies.Include(x => x.CurrentPayrollJob.PayrollJobType).Include(x => x.Institution)
                    .Where(x => x.Institution.EndDate.HasValue == false ||
                                EntityFunctions.TruncateTime(x.Institution.EndDate.Value) >=
                                EntityFunctions.TruncateTime(DateTime.Now))
                    .OrderBy(x => x.Institution.Name).ToList();
                _companies = new ObservableCollection<Company>(res);
                return res;
            }
        }

        static ObservableCollection<PayrollSetupItem> _currentSelectedPayrollSetups = new ObservableCollection<PayrollSetupItem>();
        public ObservableCollection<PayrollSetupItem> CurrentSelectedPayrollSetups
        {
            get => _currentSelectedPayrollSetups;
            set => _currentSelectedPayrollSetups = value;
        }

       
  
        private void _PayrollEmployeeSetup_CurrentChanged(object sender, EventArgs e)
        {
          
        }
        //IF YOU PUT STATIC YOU WILL LINK ALL COMBOBOXES TOGETHER!!!!!!!!!!!!!!!
        List<PayrollJobType> _payrollJobTypes;
        public List<PayrollJobType> PayrollJobTypes
        {
            get
            {
                if (_payrollJobTypes != null) return _payrollJobTypes;
                //using (var ctx = new PayrollDB())
                using (var ctx = new PayrollDB())
                {
                    _payrollJobTypes = ctx.PayrollJobTypes.ToList();
                }
                return _payrollJobTypes;
            }
        }

       
        //[MyExceptionHandlerAspect]
        private static ObservableCollection<Employee> _employees;

        public ObservableCollection<Employee> Employees
        {
            get { return _employees; }
            set
            {
                _employees = value;
                OnPropertyChanged("Employees");
            }
        }
//if (CurrentCompany == null) return null;
//                if (_employees != null) return new ObservableCollection<Employee>(_employees.Where(x => x.InstitutionId == CurrentCompany.InstitutionId).ToList());

//                return GetEmployees();
        public ObservableCollection<Employee> LoadEmployees()
        {
            try
            {

                
                

                var empLst = new ObservableCollection<Employee>();
                if (CurrentCompany == null) return empLst;
                using (var ctx = new PayrollDB())
                {
                    List<EmployeeInfo> res;
                    if(CurrentPayrollJob != null)
                    {
                        var cpjobId = CurrentPayrollJob?.PayrollJobId;
                        res = ctx.Employees
                        .Include("EmployeeAccounts.Accounts.AccountType")
                        .Where(e => (e.EmploymentEndDate.HasValue == false
                                     || EntityFunctions.TruncateTime(e.EmploymentEndDate) >=
                                     EntityFunctions.TruncateTime(DateTime.Now)))
                        .Where(x => x.CompanyId == CurrentCompany.InstitutionId)
                        .OrderBy(x => x.LastName)
                        .Select(x => new EmployeeInfo
                        {
                            FirstName = x.FirstName,
                            InstitutionId = (int) x.CompanyId,
                            DriversLicence = x.DriversLicence,
                            EmailAddress = x.EmailAddress,
                            EmployeeId = x.EmployeeId,
                            EmploymentEndDate = x.EmploymentEndDate,
                            EmploymentStartDate  = x.EmploymentStartDate,
                            LastName = x.LastName,
                            MiddleName = x.MiddleName,
                            SexId = x.SexId,
                            SupervisorId = x.SupervisorId,
                            UnionMember = x.UnionMember,
                            EmployeeAccounts = x.EmployeeAccounts,
                            Salary = x.PayrollEmployeeSetups
                                .Where(p => p.PayrollSetupItem != null &&
                                            p.PayrollSetupItem.IncomeDeduction == true &&
                                            p.PayrollSetupItem.Name == "Salary"),
                            TaxableBenefitsTotal = x.PayrollEmployeeSetups
                                .Where(p => p.PayrollSetupItem != null &&
                                            p.PayrollSetupItem.IncomeDeduction == true &&
                                            p.PayrollSetupItem.IsTaxableBenefit == true),
                            TotalIncome = (double?) x.PayrollItems
                                .Where(p => p.IncomeDeduction == true && p.ParentPayrollItem == null && p.PayrollJobId == cpjobId)
                                .DefaultIfEmpty()
                                .Sum(p => p.Amount),
                            TotalDeductions = (double?) x.PayrollItems
                                .Where(p => p.IncomeDeduction == false && p.ParentPayrollItem == null && p.PayrollJobId == cpjobId)
                                .DefaultIfEmpty()
                                .Sum(p => p.Amount),
                            PreTotalIncome = x.PayrollEmployeeSetups
                                .Where(p => p.PayrollSetupItem != null &&
                                            p.PayrollSetupItem.IncomeDeduction == true &&
                                            p.PayrollJobType.PayrollJobTypeId == CurrentPayrollJob.PayrollJobTypeId),
                            PreTotalDeductions =
                                x.PayrollEmployeeSetups
                                    .Where(p => p.PayrollSetupItem != null &&
                                                p.PayrollSetupItem.IncomeDeduction == false &&
                                                p.PayrollJobType.PayrollJobTypeId ==
                                                CurrentPayrollJob.PayrollJobTypeId)
                        }).ToList();
                        
                    }
                    else
                    {
                        res = ctx.Employees
                        .Include("EmployeeAccounts.Account.AccountType")
                        .Where(e => (e.EmploymentEndDate.HasValue == false
                                     || EntityFunctions.TruncateTime(e.EmploymentEndDate) >=
                                     EntityFunctions.TruncateTime(DateTime.Now)))
                        .Where(x => x.CompanyId == CurrentCompany.InstitutionId )
                        .OrderBy(x => x.LastName)
                        .Select(x => new EmployeeInfo
                        {
                            FirstName = x.FirstName,
                            InstitutionId = (int) x.CompanyId,
                            DriversLicence = x.DriversLicence,
                            EmailAddress = x.EmailAddress,
                            EmployeeId = x.EmployeeId,
                            EmploymentEndDate = x.EmploymentEndDate,
                            EmploymentStartDate = x.EmploymentStartDate,
                            LastName = x.LastName,
                            MiddleName = x.MiddleName,
                            SexId = x.SexId,
                            SupervisorId = x.SupervisorId,
                            UnionMember = x.UnionMember,
                            EmployeeAccounts = x.EmployeeAccounts,
                            Salary = x.PayrollEmployeeSetups
                                .Where(p => p.PayrollSetupItem != null &&
                                            p.PayrollSetupItem.IncomeDeduction == true &&
                                            p.PayrollSetupItem.Name == "Salary"),
                            TaxableBenefitsTotal = x.PayrollEmployeeSetups
                                .Where(p => p.PayrollSetupItem != null &&
                                            p.PayrollSetupItem.IncomeDeduction == true &&
                                            p.PayrollSetupItem.IsTaxableBenefit == true),
                            TotalIncome = (double?)x.PayrollItems
                                .Where(p => p.IncomeDeduction == true && p.ParentPayrollItem == null )
                                .Sum(p => p.Amount),
                            TotalDeductions = (double?)x.PayrollItems
                                .Where(p => p.IncomeDeduction == false && p.ParentPayrollItem == null )
                                .Sum(p => p.Amount),
                            PreTotalIncome = x.PayrollEmployeeSetups
                                .Where(p => p.PayrollSetupItem != null &&
                                            p.PayrollSetupItem.IncomeDeduction == true),
                            PreTotalDeductions =
                                x.PayrollEmployeeSetups
                                    .Where(p => p.PayrollSetupItem != null &&
                                                p.PayrollSetupItem.IncomeDeduction == false )
                        }).ToList();
                    }

                    foreach (var emp in res)
                    {
                        double? sum = 0;
                        foreach (var p in emp.Salary)
                            sum += p.CalcAmount;
                        double? sum1 = 0;
                        foreach (var p in emp.TaxableBenefitsTotal)
                            sum1 += p.CalcAmount;
                        var nemp = new Employee()
                        {
                            FirstName = emp.FirstName,
                            CompanyId = emp.InstitutionId,
                            DriversLicence = emp.DriversLicence,
                            EmailAddress = emp.EmailAddress,
                            EmployeeId = emp.EmployeeId,
                            EmploymentEndDate = emp.EmploymentEndDate,
                            EmploymentStartDate = emp.EmploymentStartDate,
                            LastName = emp.LastName,
                            MiddleName = emp.MiddleName,
                            SexId = emp.SexId,
                            SupervisorId = emp.SupervisorId,
                            UnionMember = emp.UnionMember,
                            Salary = (double) sum,

                            TaxableBenefitsTotal = (double) sum1,
                            TotalIncome = emp.TotalIncome.GetValueOrDefault(),
                            TotalDeductions = emp.TotalDeductions.GetValueOrDefault(),
                            PreTotalIncome = (double?) emp.PreTotalIncome?.Sum(p => p.CalcAmount),
                            PreTotalDeductions = (double?) emp.PreTotalDeductions?.Sum(p => p.CalcAmount) * -1,
                        };
                        foreach (var employeeAccount in emp.EmployeeAccounts.ToList())
                        {
                            employeeAccount.AccountReference.Load();
                            nemp.EmployeeAccounts.Add(employeeAccount);
                        }
                        empLst.Add(nemp);
                    }

                    
                }
                _employees = empLst;
                OnStaticPropertyChanged("Employees");
                GetPayrollEmployeeSetups();
                return empLst;
            }
            catch (Exception)
            {
                throw;
            }
        }


        static ObservableCollection<DataLayer.PayrollJob> _payrollJobs;
       
        public ObservableCollection<DataLayer.PayrollJob> PayrollJobs
        {
            get
            {
                if (CurrentCompany == null) return null;

               if (_payrollJobs != null) return new ObservableCollection<DataLayer.PayrollJob>(_payrollJobs.Where(x => x.CompanyId == CurrentCompany.InstitutionId).ToList());
                UpdatePayrollJobs();
                return _payrollJobs;

            }

        }

        public void UpdatePayrollJobs()
        {
            using (var ctx = new PayrollDB())
            {
                try
                {
                    _payrollJobs = new ObservableCollection<DataLayer.PayrollJob>(ctx.PayrollJobs
                        .Where(x => x.StartDate.Year == CurrentYear)
                        //.Include(x => x.PayrollJobType)
                        //.Include(x => x.PayrollItems)
                        .Select(x => new
                        {
                            x.PayrollJobId,
                            x.PayrollJobTypeId,
                            InstitutionId = x.CompanyId,
                            x.EndDate,
                            x.StartDate,
                            x.PaymentDate,
                            x.Status,
                            PayrollJobTypeName = x.PayrollJobType.Name,
                            TotalDeductions = (double?) x.PayrollItems
                                .Where(p => p.IncomeDeduction == false && p.ParentPayrollItem == null &&
                                            p.Employee != null && p.Employee.CompanyId == x.CompanyId)
                                .Sum(p => p.Amount),
                            TotalIncome = (double?) x.PayrollItems
                                .Where(p => p.IncomeDeduction == true && p.ParentPayrollItem == null &&
                                            p.Employee != null && p.Employee.CompanyId == x.CompanyId)
                                .Sum(p => p.Amount)
                        }).ToList().Select(x => new DataLayer.PayrollJob()
                        {
                            PayrollJobId = x.PayrollJobId,
                            PayrollJobTypeId = x.PayrollJobTypeId,
                            CompanyId = x.InstitutionId,
                            EndDate = x.EndDate,
                            PaymentDate = x.PaymentDate,
                            StartDate = x.StartDate,
                            Status = x.Status,
                            Name = String.Format("{2}: {0:MMM-dd-yy} - {1:MMM-dd-yy}", x.StartDate, x.EndDate,
                                x.PayrollJobTypeName),
                            TotalIncome = x.TotalIncome.GetValueOrDefault(),
                            TotalDeductions = x.TotalDeductions.GetValueOrDefault()
                        }));
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
            }
        }

        public class ComboBoxItemString
        {
            public string ValueString { get; set; }
        }



      //  ListCollectionView _Accounts;
        internal  ObservableCollection<Account> HybridAccountsLst = new ObservableCollection<Account>();


        
        private static ObservableCollection<Account> _Accounts;
        //[MyExceptionHandlerAspect]
        public  ObservableCollection<Account> Accounts => _Accounts ?? (_Accounts = GetAccountsData());

        
        //[MyExceptionHandlerAspect]
        private static ObservableCollection<Institution> _institutions;

        public ObservableCollection<Institution> Institutions
        {
            get { return _institutions; }
            set
            {
                _institutions = value;
                OnPropertyChanged("Institutions");
            }
        }

        private static ObservableCollection<Institution> _institutionsAndCompanies;

        public ObservableCollection<Institution> InstitutionsAndCompanies
        {
            get { return _institutionsAndCompanies; }
            set
            {
                _institutionsAndCompanies = value;
                OnPropertyChanged("Institutions");
            }
        }




        private ObservableCollection<Account> GetAccountsData()
        {

            try
            {
                GenerateHybridAccounts();
                OnStaticPropertyChanged("AccountsData");
                ObservableCollection<Account> lst;
                if (BaseViewModel.Instance.CurrentPayrollJob != null)
                {
                    lst = new ObservableCollection<Account>(
                              HybridAccountsLst.ToList().Where(
                                      x => x?.CurrentAccountEntries != null && x?.CurrentAccountEntries.Count > 0)
                                  .ToList()) ??
                          new ObservableCollection<Account>();
                }
                else
                {
                    lst = new ObservableCollection<Account>();
                }

                _Accounts = lst;
                OnStaticPropertyChanged(nameof(Accounts));
                return lst;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

        }

        private static ObservableCollection<DataLayer.Account> _currentAccountsLst;
        //[MyExceptionHandlerAspect]
        public ObservableCollection<DataLayer.Account> CurrentAccountsLst
        {
            get
            {
                if (_currentAccountsLst != null) return _currentAccountsLst;
                return UpdateCurrentAccountsLst();
            }
        }

        public ObservableCollection<Account> UpdateCurrentAccountsLst()
        {
            using (var ctx = new PayrollDB())
            {
                ObservableCollection<DataLayer.Account> lst = new ObservableCollection<DataLayer.Account>();

                foreach (var item in ctx.Accounts.OfType<DataLayer.InstitutionAccounts>())
                {
                    lst.Add(item);
                }
                if (CurrentEmployee != null)
                {
                    foreach (var item in CurrentEmployee.EmployeeAccounts)
                    {
                        lst.Add(item.Account);
                    }
                }
                //else
                //{
                //    foreach (var item in ctx.Accounts.OfType<DataLayer.EmployeeAccount>())
                //    {
                //        lst.Add(item);
                //    }
                //}
                _currentAccountsLst = lst;
                return _currentAccountsLst;
            }
        }

        //[MyExceptionHandlerAspect]
        internal void GenerateHybridAccounts()
        {
            try
            {
                HybridAccountsLst.Clear();
                
                using (var ctx = new PayrollDB())
                {
                    HybridAccountsLst = new ObservableCollection<Account>(ctx.Accounts
                        .OfType<Account>().Include(x => x.Institution));

                }
                CreateInstitutionEmployeeAccounts();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }


        }

        

        private int currentYear = DateTime.Now.Year;
        public int CurrentYear 
        {
            get { return currentYear; }
            set
            {
                currentYear = value;
                OnStaticPropertyChanged("CurrentYear");
                OnPropertyChanged("CurrentYear");
            } 
        }

        static DataLayer.Institution _currentInstitution = null;

        public virtual DataLayer.Institution CurrentInstitution
        {
            get
            {
                return _currentInstitution;

            }
            set
            {
                if (_currentInstitution != value)
                    _currentInstitution = value;
                OnStaticPropertyChanged("CurrentInstitution");
                OnPropertyChanged("CurrentInstitution");
            }
        }

        //static DataLayer.Account _currentAccount;
        //public virtual DataLayer.Account CurrentAccount
        //{
        //    get
        //    {
        //        if (_currentAccount != null) MergeEmployeAccountIntoAccount(_currentAccount);
        //        return _currentAccount;
        //        // return (DataLayer.Employee)GetValue(CurrentEmployeeProperty);
        //    }
        //    set
        //    {
        //        if (_currentAccount == null && value != null)
        //        {
        //            _currentAccount = value;
                    
        //            CurrentAccount.AccountEntries.AssociationChanged += AssociationChanged;
        //        }
        //        _currentAccount = value;
        //        OnStaticPropertyChanged("CurrentAccount");
        //        OnStaticPropertyChanged("Accounts");
        //        OnStaticPropertyChanged("AccountTypes");

        //        // SetValue(CurrentEmployeeProperty, value);
        //    }
        //}

       

        static DataLayer.EmployeeAccount _currentEmployeeAccount;
        public virtual DataLayer.EmployeeAccount CurrentEmployeeAccount
        {
            get
            {
                return _currentEmployeeAccount;
                // return (DataLayer.Employee)GetValue(CurrentEmployeeProperty);
            }
            set
            {
                if (_currentEmployeeAccount == null && value != null)
                {
                    _currentEmployeeAccount = value;
                    CurrentEmployeeAccount.Account.AccountEntries.AssociationChanged += AssociationChanged;
                }
                _currentEmployeeAccount = value;
                OnPropertyChanged("CurrentEmployeeAccount");
                OnStaticPropertyChanged("CurrentEmployeeAccount");
                OnStaticPropertyChanged("Accounts");
                OnStaticPropertyChanged("AccountTypes");

                // SetValue(CurrentEmployeeProperty, value);
            }
        }

        static DataLayer.Account _currentAccount;
        public virtual DataLayer.Account CurrentAccount
        {
            get
            {
                return _currentAccount;
                // return (DataLayer.Employee)GetValue(CurrentEmployeeProperty);
            }
            set
            {
                if(_currentAccount == null && value != null)
                {
                    _currentAccount = value;
                    CurrentAccount.AccountEntries.AssociationChanged += AssociationChanged;
                }
                _currentAccount = value;
                OnStaticPropertyChanged("CurrentAccount");
               // OnStaticPropertyChanged("Accounts");
               // OnStaticPropertyChanged("AccountTypes");

                // SetValue(CurrentEmployeeProperty, value);
            }
        }

        static DataLayer.Employee _currentEmployee;
        
        public virtual DataLayer.Employee CurrentEmployee
        {
            get
            {
               return _currentEmployee;
              // return (DataLayer.Employee)GetValue(CurrentEmployeeProperty);
            }
            set
            {
                if (_currentEmployee != value)
                {
                    _currentEmployee = value;
                    OnStaticPropertyChanged("CurrentEmployee");
                    OnStaticPropertyChanged("CurrentPayrollEmployeeSetup");
                }
                if (_currentEmployee == null & value != null )
                {
                   // _currentEmployee = value;
                    CurrentEmployee.CompanyReference.AssociationChanged += AssociationChanged;
                    CurrentEmployee.EmployeeAccounts.AssociationChanged += AssociationChanged;
                    CurrentEmployee.Employees.AssociationChanged += AssociationChanged;
                    CurrentEmployee.PayrollEmployeeSetups.AssociationChanged += AssociationChanged;
                    CurrentEmployee.PayrollItems.AssociationChanged += AssociationChanged;
                    CurrentEmployee.SupervisorReference.AssociationChanged += AssociationChanged;
                }
               
                //OnStaticPropertyChanged("IncomePayrollLineItems");
                //OnStaticPropertyChanged("DeductionPayrollLineItems");
               // SetValue(CurrentEmployeeProperty, value);
            }
        }



        static DataLayer.PayrollItem _currentPayrollItem;
        public virtual DataLayer.PayrollItem CurrentPayrollItem
        {
            get
            {
                return _currentPayrollItem;
               
            }
            set
            {
                _currentPayrollItem = value;
                OnPropertyChanged("CurrentPayrollItem");
                OnStaticPropertyChanged("CurrentPayrollItem");
             
            }
        }

         DataLayer.PayrollEmployeeSetup _currentPayrollEmployeeSetup;
        public virtual DataLayer.PayrollEmployeeSetup CurrentPayrollEmployeeSetup
        {
            get
            {
                return _currentPayrollEmployeeSetup;
               
            }
            set
            {
                  _currentPayrollEmployeeSetup = value;
                OnStaticPropertyChanged("CurrentPayrollEmployeeSetup");
            }
        }

        static DataLayer.PayrollSetupItem _currentPayrollSetupItem;
        public virtual DataLayer.PayrollSetupItem CurrentPayrollSetupItem
        {
            get
            {
                return _currentPayrollSetupItem;
            }
            set
            {
                _currentPayrollSetupItem = value;

                OnStaticPropertyChanged("CurrentPayrollSetupItem");
            }
        }

        static DataLayer.Company _currentCompany;
       
        public  Company CurrentCompany
        {
            get
            {
                return _currentCompany;
            }
            set
            {
                _currentCompany = value;
                //
                if (_currentCompany != null && _currentCompany.CurrentPayrollJob != null)
                {
                    if (CurrentPayrollJob != _currentCompany.CurrentPayrollJob)
                    {
                        CurrentPayrollJob = _currentCompany.CurrentPayrollJob;
                        _currentPayrollJobType = _currentCompany.CurrentPayrollJob.PayrollJobType;

                    }
                    else
                    {
                        _Accounts = null;
                        OnStaticPropertyChanged("Accounts");
                    }
                }
                else
                {
                    
                    _Accounts = null;
                    OnStaticPropertyChanged("Accounts");
                }
                LoadEmployees();
                LoadPayrollSetupItems();
                OnStaticPropertyChanged("CurrentCompany");
                OnStaticPropertyChanged("Employees");
                OnStaticPropertyChanged("CurrentEmployee");
                

               

            }
        }

        static DataLayer.PayrollJob _currentPayrollJob;

        //public static DataLayer.PayrollJob CurrentPayrollJob
        //{
        //    get
        //    {
        //        return _currentPayrollJob;
        //    }
        //}

        static DataLayer.PayrollJobType _currentPayrollJobType;
        public  DataLayer.PayrollJobType CurrentPayrollJobType
        {
            get
            {
                return _currentPayrollJobType;
            }
            set
            {
                _currentPayrollJobType = value;
                OnStaticPropertyChanged("CurrentPayrollJobType");
            }
        }

        public DataLayer.PayrollJob CurrentPayrollJob
        {
            get { return _currentPayrollJob; }
            set
            {
                if (_currentPayrollJob == value) return;
                _currentPayrollJob = value;
                if (_currentPayrollJob != null && CurrentCompany.CurrentPayrollJob != CurrentPayrollJob)
                {
                    using (var ctx = new PayrollDB())
                    {
                        var ritm = ctx.Companies.First(x => x.InstitutionId == CurrentCompany.InstitutionId);
                        CurrentCompany.CurrentPayrollJobId = _currentPayrollJob.PayrollJobId;
                        ctx.Companies.Attach(ritm);
                        ctx.Companies.ApplyCurrentValues(CurrentCompany);
                        SaveDatabase(ctx);

                        //CurrentCompany = ctx.Branches.Include(x => x.CurrentPayrollJob).First(x => x.InstitutionId == CurrentCompany.InstitutionId);
                    }

                }
                if (_currentPayrollJob != null)
                {
                    Task.Run(() =>
                    {
                        LoadEmployees();
                        
                    });

                    Task.Run(() =>
                    {
                        GetAccountsData();

                    });

                    

                }
                OnPropertyChanged("CurrentPayrollJob");
                OnStaticPropertyChanged("CurrentPayrollJob");

            }
        }

        

        public DateTime Date => DateTime.Now.Date;
        

        //[MyExceptionHandlerAspect]
        internal  void CycleCurrentCompany(int InstitutionId)
        {
            _currentCompany = Companies.FirstOrDefault(x => x.InstitutionId == InstitutionId);
            OnStaticPropertyChanged("CurrentCompany");

        }

        internal void CycleCurrentEmployee(int empId)
        {
            _currentEmployee = Employees.FirstOrDefault(x => x.EmployeeId == empId);
            OnStaticPropertyChanged("CurrentEmployee");
        }

        internal void CycleCurrentInstitution(int instId)
        {
            _currentInstitution = Institutions.FirstOrDefault(x => x.InstitutionId == instId);
            OnStaticPropertyChanged("CurrentInstitution");
        }
        //[MyExceptionHandlerAspect]
        internal void CycleCurrentPayrollJob()
        {
            DataLayer.PayrollJob b = _currentPayrollJob;
            
            _currentPayrollJob = PayrollJobs.FirstOrDefault();
           
            OnStaticPropertyChanged("CurrentPayrollJob");
            _currentPayrollJob = b;
            OnStaticPropertyChanged("CurrentPayrollJob");

        }
        //[MyExceptionHandlerAspect]
        //internal  void CycleAccounts()
        //{
        //    _currentAccount = null;
        //    OnStaticPropertyChanged("CurrentAccount");
        //    if (_currentAccount == null) _currentAccount = Accounts.FirstOrDefault();
        //    OnStaticPropertyChanged("CurrentAccount");
        //    OnStaticPropertyChanged("Accounts");
        //}

        #region INotifyPropertyChanged
        public static event PropertyChangedEventHandler staticPropertyChanged;
        public static void OnStaticPropertyChanged(String info)
        {
            staticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(info));
        }


        public  event PropertyChangedEventHandler PropertyChanged;
        public void OnPropertyChanged(String info)
        {
            
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
            
        }
        #endregion

        //[MyExceptionHandlerAspect]

        public enum TriBoolState
        {
            Fail,
            Success,
            Continue
        }
        //[MyExceptionHandlerAspect]
        public TriBoolState ConfigPayrollItem(PayrollItem pi, DataLayer.PayrollEmployeeSetup item, bool AddToCurrentPayrollJob)
        {
            
            pi.IsTaxableBenefit = item.PayrollSetupItem.IsTaxableBenefit;
            pi.ApplyToTaxableBenefits = item.PayrollSetupItem.ApplyToTaxableBenefits;
            if(item.BaseAmount != null) pi.BaseAmount = (double)item.BaseAmount;
            pi.Amount = Convert.ToDouble(item.Amount);

            pi.Priority = item.PayrollSetupItem.Priority;
            if (AddToCurrentPayrollJob == true && CurrentPayrollJob != null)  pi.PayrollJobId = CurrentPayrollJob.PayrollJobId;
            pi.Name = item.PayrollSetupItem.Name;
            pi.PayrollSetupItemId = item.PayrollSetupItemId;
            pi.EmployeeId = item.EmployeeId;
            pi.IncomeDeduction = item.PayrollSetupItem.IncomeDeduction;
            pi.Rate = Convert.ToSingle(item.Rate);
            pi.RateRounding = item.RateRounding;
            
            pi.Status = "Generated";
            // do accounts
            if (pi.EmployeeId != item.EmployeeId) return TriBoolState.Continue;

          //  DataLayer.EmployeeAccount ea = item.EmployeeAccount; // GetEmployeeAccount(item.PayrollSetupItem.EmployeeAccountType, item.EmployeeId);
            if (item.CreditAccountId == 0)
            {
                //MessageBox.Show(string.Format("{0} has no Debit Account setup for Account Type:{1}", item.Employee.DisplayName, item.PayrollSetupItem.EmployeeAccountType));
                
                return TriBoolState.Fail;
            }

            if (item.DebitAccountId == 0)
            {
               // MessageBox.Show(string.Format("{0} has no Credit Account setup for Account Type:{1}", item.Employee.DisplayName, item.PayrollSetupItem.EmployeeAccountType));
                return TriBoolState.Fail;
            }

            //if (item.PayrollSetupItem.IncomeDeduction == true)
            //{
                pi.CreditAccountId = item.CreditAccountId; //ea.AccountId;
                pi.DebitAccountId = item.DebitAccountId; //item.PayrollSetupItem.PayrollItemAccountId;
            //}
            //else
            //{

            //    pi.CreditAccountId = item.PayrollSetupItem.PayrollItemAccountId;
            //    pi.DebitAccountId = ea.AccountId;
            //}
            return TriBoolState.Success;
        }
        //[MyExceptionHandlerAspect]
        public void PostToAccounts()
        {
            if (CurrentPayrollJob.Status == "Posted")
            {
                MessageBox.Show("Sorry Payroll Job already Posted! Try creating a new Job.", "Payroll Job already posted");
                return;
            }

           // SetPayrollItemsAmounts();

            ClearExistingAccountEntries();
           
            using (var ctx = new PayrollDB())
            {
                  


                foreach (var item in ctx.PayrollItems
                    .Where(p => p.PayrollJobId == CurrentPayrollJob.PayrollJobId && p.Status == "Amounts Processed"
                                                    //&& p.ParentPayrollItem == null
                                ).ToList())
                {

                    // do debit account entry
                    CreateAccountEntry(item, true, ctx);


                    // do credit account entry
                    CreateAccountEntry(item, false, ctx);
                    item.Status = "Posted";

                    if (item.PayrollSetupItem != null && item.PayrollSetupItem.CompanyLineItemDescription != "")
                    {
                        foreach (var citm in item.PayrollItems)
                        {
                            // do debit account entry
                            CreateAccountEntry(citm, true, ctx);


                            // do credit account entry
                            CreateAccountEntry(citm, false, ctx);
                        }
                    }

                }
                SaveDatabase(ctx);
}
            var cp = CurrentPayrollJob;
            CurrentPayrollJob.Status = "Posted";
            CurrentPayrollJob.PreparedBy = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            SavePayrollJob();
            UpdatePayrollJobs();

            LoadEmployees();
            _Accounts = null;
            CurrentPayrollJob = cp;
            MessageBox.Show("Payroll Job Posted");
            OnStaticPropertyChanged("CurrentPayrollJob");
            OnStaticPropertyChanged("PayrollItems");
            OnStaticPropertyChanged("Employees");
            
            OnStaticPropertyChanged("CurrentEmployee.EmployeeAccounts");
            OnStaticPropertyChanged("EmployeeAccounts");
            OnStaticPropertyChanged("CurrentAccount");
            
            OnStaticPropertyChanged("Accounts");
            OnStaticPropertyChanged("CurrentEmployeeAccount");

        }
        //[MyExceptionHandlerAspect]
        //[MyExceptionHandlerAspect]
        private void ClearExistingAccountEntries()
        {
            using (var ctx = new PayrollDB())
            {
                foreach (var item in ctx.AccountEntries
                    .Where(ae => ae.PayrollItem.PayrollJobId == CurrentPayrollJob.PayrollJobId).ToList())

                {
                    ctx.AccountEntries.DeleteObject(item);
                }
                SaveDatabase(ctx);
            }
        }
        //[MyExceptionHandlerAspect]
        //private void CycleCurrentEmployee()
        //{
        //    try
        //    {
        //        CurrentEmployee = new Employee();
        //        OnStaticPropertyChanged("CurrentEmployee");
        //        if (CurrentEmployee == null) CurrentEmployee = Employees.FirstOrDefault();
        //        OnStaticPropertyChanged("CurrentEmployee");
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //        throw;
        //    }

        //}

        //[MyExceptionHandlerAspect]
        private void CreateAccountEntry(PayrollItem item, bool debitCredit, PayrollDB ctx)
        {

                DataLayer.AccountEntry ea;


                DataLayer.AccountEntry ae = ctx.AccountEntries.CreateObject();

                if (debitCredit == true)
                {
                    ea = ctx.AccountEntries
                        .FirstOrDefault(a => a.PayrollItem.PayrollJobId == CurrentPayrollJob.PayrollJobId &&
                                    a.PayrollItemId == item.PayrollItemId && a.AccountId == item.DebitAccountId);
                    if (ea != null) ctx.AccountEntries.DeleteObject(ea);

                    ae.AccountId = item.DebitAccountId;
                    ae.DebitAmount = item.Amount;
                }
                else
                {
                    ea = ctx.AccountEntries
                        .FirstOrDefault(a => a.PayrollItem.PayrollJobId == CurrentPayrollJob.PayrollJobId &&
                                    a.PayrollItemId == item.PayrollItemId && a.AccountId == item.CreditAccountId);
                    if (ea != null) ctx.AccountEntries.DeleteObject(ea);

                    ae.AccountId = item.CreditAccountId;
                    ae.CreditAmount = item.Amount;
                }

                ae.PayrollItemId = item.PayrollItemId;
                ae.TradeDate = CurrentPayrollJob.PaymentDate;
                ae.EntryTime = DateTime.Now;

                ctx.AccountEntries.AddObject(ae);
                
            
        }


        //[MyExceptionHandlerAspect]
        public static void SaveDatabase(PayrollDB ctx)
        {
            try
            {
                ctx.SaveChanges();
                ctx.AcceptAllChanges();
            }
            catch (System.Data.OptimisticConcurrencyException oce)
            {
                foreach (var item in oce.StateEntries)
                {
                    if (item.State == EntityState.Deleted)
                    {
                        ctx.Detach(item.Entity);
                    }
                    else
                    {
                        ctx.Refresh(RefreshMode.StoreWins, item);
                        //  SaveDatabase();
                    }
                }
                SaveDatabase(ctx);
            }
            catch (System.Data.UpdateException ue)
            {
                foreach (var item in ue.StateEntries)
                {
                    if (item.State == EntityState.Added || item.State == EntityState.Deleted)
                    {
                        ctx.Detach(item.Entity);
                    }
                    else
                    {
                       // db.DeleteObject(item.Entity);
                        throw ue;
                    }
                    
                        SaveDatabase(ctx);
                   
                }

            }
            catch (System.InvalidOperationException ie)
            {
                //throw ie;
                //ResetDatabase(ctx);
            }
            catch (Exception e)
            {
                //ResetDatabase(ctx);
            }
        }

        //[MyExceptionHandlerAspect]
        //private static DataLayer.EmployeeAccount GetEmployeeAccount(string accountType, int empid)
        //{
        //    using (var ctx = new PayrollDB())
        //    {
        //        DataLayer.EmployeeAccount empacc = (from a in ctx.Accounts.OfType<DataLayer.EmployeeAccount>().Include(x => x.AccountTypes)
        //            where a.EmployeeId == empid && a.AccountTypes.Name == accountType
        //            select a).FirstOrDefault();
        //        return empacc;

        //    }

        //}

        //[MyExceptionHandlerAspect]

        //[MyExceptionHandlerAspect]


        //[MyExceptionHandlerAspect]
        //[MyExceptionHandlerAspect]
        //[MyExceptionHandlerAspect]

        //[MyExceptionHandlerAspect]
        public  void SetupAllEmployees()
        {
            if (CurrentPayrollJobType == null)
            {
                MessageBox.Show("Please Select the Payroll Job Type before processing");
                return;
            }
            foreach (var emp in _employees.ToList())
            {
               if( AutoSetupEmployee(emp) == false) break;
            }
            MessageBox.Show("Setup Complete");
        }

        public void SetEmployeeSetupBaseAmounts(int empId)
        {
            using (var ctx = new PayrollDB())
            {
                var salary = ctx.PayrollEmployeeSetups
                    .Include(x => x.PayrollSetupItem)
                    .Where(p => 
                                p.EmployeeId == empId &&
                                p.PayrollSetupItem != null &&
                                p.PayrollSetupItem.IncomeDeduction == true &&
                                p.PayrollSetupItem.Name == "Salary").ToList().Sum(p => p.CalcAmount);
                var taxableBenefitsTotal = ctx.PayrollEmployeeSetups
                                            .Include(x => x.PayrollSetupItem)
                                            .Where(p => p.EmployeeId == empId &&
                                                        p.PayrollSetupItem != null &&
                                                        p.PayrollSetupItem.IncomeDeduction == true &&
                                                        p.PayrollSetupItem.IsTaxableBenefit == true &&
                                                        p.PayrollSetupItem.Name != "Salary").ToList().Select(x => x.CalcAmount).DefaultIfEmpty(0).Sum();

                var payrollEmployeeSetups = ctx.PayrollEmployeeSetups.Include(x => x.PayrollSetupItem)
                    .Where(p => p.EmployeeId == empId)
                                            //&&
                                            //(p.BaseAmount != (p.PayrollSetupItem.ApplyToTaxableBenefits == true
                                            //     ? salary + taxableBenefitsTotal
                                            //     : salary) || p.BaseAmount == 0)
                   .ToList();


                
                foreach (var item in payrollEmployeeSetups)
                {
                    item.BaseAmount = (item.PayrollSetupItem.ApplyToTaxableBenefits == true
                        ? salary + taxableBenefitsTotal
                        : salary);
                    var dbitm = ctx.PayrollEmployeeSetups.First(
                        x => x.PayrollEmployeeSetupId == item.PayrollEmployeeSetupId);
                    dbitm.BaseAmount = item.BaseAmount;
                }
                SaveDatabase(ctx);
            }
            GetPayrollEmployeeSetups();

        }

       // private ObservableCollection<PayrollSetupItem> _payrollSetupItems;

        private static ObservableCollection<PayrollSetupItem> _payrollSetupItems;

        public ObservableCollection<PayrollSetupItem> PayrollSetupItems
        {
            get { return _payrollSetupItems; }
            set
            {
                _payrollSetupItems = value;
                OnPropertyChanged("PayrollSetupItems");
            }
        }


        //if (_payrollSetupItems != null) return _payrollSetupItems;
        //using (var ctx = new PayrollDB())
        //{
        //    _payrollSetupItems = new ObservableCollection<DataLayer.PayrollSetupItem>(ctx
        //        .PayrollSetupItems.ToList());
        //}
        //return _payrollSetupItems;


        public ObservableCollection<PayrollEmployeeSetup> GetPayrollEmployeeSetups()
        {
            if (CurrentEmployee == null || CurrentPayrollJobType == null)
            {
                _payrollEmployeeSetups = new ObservableCollection<PayrollEmployeeSetup>();
            }
            else
            {
                using (var ctx = new PayrollDB())
                {
                    _payrollEmployeeSetups = new ObservableCollection<DataLayer.PayrollEmployeeSetup>(ctx
                        .PayrollEmployeeSetups
                        .Include(x => x.PayrollSetupItem)
                        .Where(p => p.PayrollJobTypeId == CurrentPayrollJobType.PayrollJobTypeId &&
                                    p.EmployeeId == CurrentEmployee.EmployeeId));
                }
            }
            OnPropertyChanged("PayrollEmployeeSetups");
            OnStaticPropertyChanged("PayrollEmployeeSetups");
            return _payrollEmployeeSetups;
        }

        private static ObservableCollection<DataLayer.PayrollEmployeeSetup> _payrollEmployeeSetups = null;
        private readonly GeneratePayrollItems _generatePayrollItems;

        public ObservableCollection<DataLayer.PayrollEmployeeSetup> PayrollEmployeeSetups
        {
            get
            {
                if (_payrollEmployeeSetups == null) return GetPayrollEmployeeSetups();
                return _payrollEmployeeSetups;
            }
        }

        public GeneratePayrollItems GeneratePayrollItems1
        {
            get { return _generatePayrollItems; }
        }


        public bool AutoSetupEmployee(DataLayer.Employee cemp)
        {
            using (var ctx = new PayrollDB())
            {
            
            if (cemp == null)
            {
                MessageBox.Show("Please Select an Employee before processing");
                return false;
            }
            if (CurrentPayrollJobType == null)
            {
                MessageBox.Show("Please Select the Payroll Job Type before processing");
                return false;
            }
            if (_currentSelectedPayrollSetups.Count == 0)
            {
                foreach (var item in ctx.PayrollSetupItems.Where(p => p.Name.Trim().ToUpper() != "Salary".ToUpper()))
                {
                    _currentSelectedPayrollSetups.Add(item);
                }
            }
            // generate salary item
            //generate NIS
            //   UpdateEmployee();

            //foreach (var item in cemp.PayrollEmployeeSetups.AsEnumerable().Where(es => es.PayrollJobType == _currentPayrollJobType).ToList())
            //{
            //    db.DeleteObject(item);
            //}

            foreach (var ea in cemp.EmployeeAccounts)
            {
                // get salary item
                var salitm = (from p in ctx.PayrollSetupItems
                              where p.Name.Trim().ToUpper() == "Salary".ToUpper()
                              select p).FirstOrDefault();

                if (salitm != null)
                {
                    DataLayer.PayrollEmployeeSetup nes = AutoSetupSalary(cemp, ea, salitm, ctx);
                    foreach (var ps in CurrentSelectedPayrollSetups.Where(p => p.Name.Trim().ToUpper() != "Salary".ToUpper()))
                    {
                        if (AutoSetupPayrollItem(cemp, ea, nes, ps, ctx) == false) return false;
                    }

                }
                else
                {
                    System.Windows.MessageBox.Show("Please Create a 'Salary' Payroll Item");
                    return false;
                }



            }
            
                SaveDatabase(ctx);
            }

            SetEmployeeSetupBaseAmounts(cemp.EmployeeId);
            return true;
        }

      


        private  bool AutoSetupPayrollItem(Employee cemp, EmployeeAccount ea, PayrollEmployeeSetup nes, PayrollSetupItem ps, PayrollDB ctx)
        {
            // get NIS item
           
                
                if ((ps.Amount != null && ps.Amount != 0) && (ps.Rate != null && ps.Rate != 0))
                {
                    MessageBox.Show(string.Format("For '{0}' both Rate and Amount cannot contain Values, please delete the unused value",ps.Name));
                    return false;
                }

                DataLayer.PayrollEmployeeSetup nis = ctx.PayrollEmployeeSetups.FirstOrDefault(x => x.PayrollSetupItemId == ps.PayrollSetupItemId && x.EmployeeId == nes.EmployeeId && x.PayrollJobTypeId == nes.PayrollJobTypeId);

                if (nis == null)
                {
                    nis = ctx.PayrollEmployeeSetups.CreateObject();
                    ctx.PayrollEmployeeSetups.AddObject(nis);
                }

                nis.Employee = cemp;

                if (ps.Amount != null)
                {
                    nis.ChargeTypeId = ctx.ChargeTypes.First(x => x.Name == "Amount").ChargeTypeId ;
                    nis.Amount = ps.Amount;
                    nis.CompanyAmount = ps.CompanyAmount;
                }

                

                if (nes.Amount > ps.RateCeiling)
                {
                    nis.ChargeTypeId = ctx.ChargeTypes.First(x => x.Name == "Amount").ChargeTypeId;
                    nis.Amount = ps.RateCeilingAmount;
                    nis.CompanyAmount = ps.RateCeilingCompanyAmount;
                }
                else
                {
                    nis.ChargeTypeId = ctx.ChargeTypes.First(x => x.Name == "Rate").ChargeTypeId;
                    nis.Rate = Convert.ToSingle(ps.Rate);
                    nis.CompanyRate = Convert.ToSingle(ps.CompanyRate);
                }
                //nis.EmployeeAccount = ea;
                //nis.EmployeeAccountId = ea.AccountId;

                //nis.IsTaxableBenefit = ps.IsTaxableBenefit;
                //nis.ApplyToTaxableBenefits = ps.ApplyToTaxableBenefits;

               SetEmployeePayrollSetupAccounts(ea, ps, nis);

                nis.PayrollSetupItem = ps;
                nis.PayrollSetupItemId = ps.PayrollSetupItemId;
                nis.PayrollJobTypeId = CurrentPayrollJobType.PayrollJobTypeId;//db.PayrollJobTypes.Where(pj => pj.Name == ps.Jobtype).FirstOrDefault().PayrollJobTypeId;

                nis.RateRounding = ps.RateRounding;

                nis.BaseAmount = nes.Amount * CurrentPayrollJobType.PayPeriods;
                
                nis.StartDate = cemp.EmploymentStartDate;

                
                        
            return true;
        }
        //[MyExceptionHandlerAspect]
        private static void SetEmployeePayrollSetupAccounts(EmployeeAccount ea, PayrollSetupItem ps, PayrollEmployeeSetup nis)
        {
            if (ps.IncomeDeduction == true)
            {
                nis.CreditAccount = ea.Account;
                nis.CreditAccountId = ea.AccountId;
                nis.DebitAccount = nis.DebitAccount;
                nis.DebitAccountId = nis.DebitAccountId;
            }
            else
            {
                nis.CreditAccount = nis.CreditAccount;
                nis.CreditAccountId = nis.CreditAccountId;
                nis.DebitAccount = ea.Account;
                nis.DebitAccountId = ea.AccountId;
            }
        }
        //[MyExceptionHandlerAspect]
        private  PayrollEmployeeSetup AutoSetupSalary(Employee cemp, EmployeeAccount ea, PayrollSetupItem salitm, PayrollDB ctx)
        {

            DataLayer.PayrollEmployeeSetup nes = ctx.PayrollEmployeeSetups.FirstOrDefault(x => x.PayrollSetupItemId == salitm.PayrollSetupItemId && x.EmployeeId == cemp.EmployeeId && x.PayrollJobTypeId == CurrentPayrollJobType.PayrollJobTypeId && x.CreditAccountId == ea.AccountId);
            if (nes != null)
            {
                return nes;
            }
            nes = ctx.PayrollEmployeeSetups.CreateObject();
            nes.Employee = cemp;
            if (ea.SalaryAssignment != null) nes.Amount = ea.SalaryAssignment;
            nes.PayrollSetupItem = salitm;
            nes.PayrollSetupItemId = salitm.PayrollSetupItemId;
            nes.ChargeTypeId = ctx.ChargeTypes.First(x => x.Name == "Amount").ChargeTypeId;
            nes.StartDate = cemp.EmploymentStartDate;
            nes.PayrollEmployeeSetupId = 0;
            nes.PayrollJobTypeId = CurrentPayrollJobType.PayrollJobTypeId; // db.PayrollJobTypes.Where(pj => pj.Name == "Salary").FirstOrDefault().PayrollJobTypeId;
           
            SetEmployeePayrollSetupAccounts(ea,salitm,nes);

            ctx.PayrollEmployeeSetups.AddObject(nes);
            return nes;
        }



    }

    public class EmployeeInfo
    {
        public string FirstName { get; set; }
        public int InstitutionId { get; set; }
        public string DriversLicence { get; set; }
        public string EmailAddress { get; set; }
        public int EmployeeId { get; set; }
        public DateTime? EmploymentEndDate { get; set; }
        public DateTime EmploymentStartDate { get; set; }
        public string LastName { get; set; }
        public int? SexId { get; set; }
        public int? SupervisorId { get; set; }
        public string MiddleName { get; set; }
        public bool? UnionMember { get; set; }
        public EntityCollection<EmployeeAccount> EmployeeAccounts { get; set; }
        public IEnumerable<PayrollEmployeeSetup> Salary { get; set; }
        public IEnumerable<PayrollEmployeeSetup> TaxableBenefitsTotal { get; set; }
        public double? TotalIncome { get; set; }
        public double? TotalDeductions { get; set; }
        public IEnumerable<PayrollEmployeeSetup> PreTotalIncome { get; set; }
        public IEnumerable<PayrollEmployeeSetup> PreTotalDeductions { get; set; }
    }
}
