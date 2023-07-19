using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using PayrollManager.DataLayer;

namespace PayrollManager
{
    public class GeneratePayrollItems
    {
        private BaseViewModel _baseViewModel;

        public GeneratePayrollItems(BaseViewModel baseViewModel)
        {
            _baseViewModel = baseViewModel;
        }

        public void Execute()
        {
            try
            {


                if (IsCurrentPayrollJobNull()) return;

                if (IsCurrentPayrollJobPosted()) return;

                if (generatePayrollItems()) return;

                SetPayrollItemsAmounts();
                _baseViewModel.LoadEmployees();

                BaseViewModel.OnStaticPropertyChanged("CurrentPayrollJob");
                BaseViewModel.OnStaticPropertyChanged("PayrollItems");

                BaseViewModel.OnStaticPropertyChanged("Employees");
                BaseViewModel.OnStaticPropertyChanged("CurrentEmployee.EmployeeAccounts");
                BaseViewModel.OnStaticPropertyChanged("CurrentEmployee");
                BaseViewModel.OnStaticPropertyChanged("PayrollJobs");
                MessageBox.Show("Payroll Job Setup Complete");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private bool generatePayrollItems()
        {
            using (var ctx = new PayrollDB())
            {
                var pitmlst = GetPayrollEmployeeSetups(ctx);
                if (HasSetupPayrollItems(pitmlst)) return true;

                if (IsSalaryFirstSetupItem(pitmlst)) return true;

                if (pitmlst.Any(item => CreatePayrollItems(item, ctx)))
                {
                    return true;
                }

                BaseViewModel.SaveDatabase(ctx);
            }

            return false;
        }

        private bool CreatePayrollItems(PayrollEmployeeSetup item, PayrollDB ctx)
        {
            if (!IsEmployeeCompanyCurrentCompany(item)) return false;
            if (!IsEmployeeEmployedDuringPayrollJob(item)) return false;

            if (!IsPayrollItemStartAfterJob(item)) return false;

            var pi = GetPayrollItem(item, ctx);


            if (ConfigurePayrollItem(item, pi, out var hasPayrollItems)) return hasPayrollItems;


            BaseViewModel.SaveDatabase(ctx); // save to get itemid
            foreach (var ci in pi.PayrollItems.ToList())
            {
                ctx.PayrollItems.DeleteObject(ci);
            }


            if (!CreateCompanyPayrollItems(item, ctx, pi)) return false;
            return false;
        }

        private bool CreateCompanyPayrollItems(PayrollEmployeeSetup item, PayrollDB ctx, PayrollItem pi)
        {
            if (string.IsNullOrEmpty(item.PayrollSetupItem.CompanyLineItemDescription)) return false;
            var companyAccount = ctx.Accounts.OfType<InstitutionAccounts>()
                .FirstOrDefault(x => x.PayeeInstitutionId == item.Employee.CompanyId);
            PayrollItem citem = CreateCompanyPayrollItem(pi, item, companyAccount);

            ctx.PayrollItems.AddObject(citem);
            return true;
        }

        private bool ConfigurePayrollItem(PayrollEmployeeSetup item, PayrollItem pi, out bool hasPayrollItems)
        {
            BaseViewModel.TriBoolState triBool = _baseViewModel.ConfigPayrollItem(pi, item, true);
            switch (triBool)
            {
                case BaseViewModel.TriBoolState.Fail:
                {
                    hasPayrollItems = true;
                    return true;
                }

                case BaseViewModel.TriBoolState.Success:
                    break;
                case BaseViewModel.TriBoolState.Continue:
                {
                    hasPayrollItems = false;
                    return true;
                }

                default:
                    break;
            }
            hasPayrollItems = false;
            return false;
        }

        private PayrollItem GetPayrollItem(PayrollEmployeeSetup item, PayrollDB ctx)
        {
            var pi = GetExistingPayrollItem(item, ctx);
            pi = CreateNewPayrollItem(ctx, pi);
            return pi;
        }

        private static PayrollItem CreateNewPayrollItem(PayrollDB ctx, PayrollItem pi)
        {
            if (pi == null)
            {
                pi = ctx.PayrollItems.CreateObject();
                ctx.PayrollItems.AddObject(pi);
            }

            return pi;
        }

        private PayrollItem GetExistingPayrollItem(PayrollEmployeeSetup item, PayrollDB ctx)
        {
            var pi = (from p in ctx.PayrollItems
                    .Include(x => x.PayrollItems)
                    .Where(p => p.PayrollJobId == _baseViewModel.CurrentPayrollJob.PayrollJobId
                                && p.PayrollJob.CompanyId == _baseViewModel.CurrentCompany.InstitutionId
                                && p.Employee.CompanyId == _baseViewModel.CurrentCompany.InstitutionId
                                && p.EmployeeId == item.EmployeeId
                                && p.PayrollSetupItemId == item.PayrollSetupItemId
                                && (p.CreditAccountId == item.CreditAccountId &&
                                    p.DebitAccountId == item.DebitAccountId)
                    )
                select p).FirstOrDefault();
            return pi;
        }

        private bool IsPayrollItemStartAfterJob(PayrollEmployeeSetup item)
        {
            if (_baseViewModel.CurrentPayrollJob.StartDate >= item.StartDate)
            {
                if (item.EndDate != null && _baseViewModel.CurrentPayrollJob.EndDate > item.EndDate)
                {
                    return false;
                }
            }
            else
            {
                MessageBox.Show(string.Format(
                    "{1} - Payroll Item-'{0}' was not created because payroll job was too early",
                    item.PayrollSetupItem.Name, item.Employee.DisplayName));
                return false;
            }

            return true;
        }

        private bool IsEmployeeEmployedDuringPayrollJob(PayrollEmployeeSetup item)
        {
            if (item.Employee.EmploymentEndDate.HasValue == true &&
                item.Employee.EmploymentEndDate.Value.Date <= _baseViewModel.CurrentPayrollJob.StartDate.Date) return false;
            return true;
        }

        private bool IsEmployeeCompanyCurrentCompany(PayrollEmployeeSetup item)
        {
            if (item.Employee.CompanyId != _baseViewModel.CurrentCompany.InstitutionId) return false;
            return true;
        }

        private static bool IsSalaryFirstSetupItem(List<PayrollEmployeeSetup> pitmlst)
        {
            if (pitmlst.First().PayrollSetupItem.Name.Trim().ToUpper() != "Salary".ToUpper())
            {
                MessageBox.Show("Salary is not the First item. Please check Payroll Item order");
                return true;
            }

            return false;
        }

        private static bool HasSetupPayrollItems(List<PayrollEmployeeSetup> pitmlst)
        {
            if (!pitmlst.Any())
            {
                MessageBox.Show("There are No Employee Payroll Setup Items for This Payroll Job Type");
                return true;
            }

            return false;
        }

        private List<PayrollEmployeeSetup> GetPayrollEmployeeSetups(PayrollDB ctx)
        {
            var pitmlst = (from p in ctx.PayrollEmployeeSetups
                    .Include(x => x.Employee)
                    .Include(x => x.PayrollSetupItem)
                    .Where(p => p.PayrollJobTypeId == _baseViewModel.CurrentPayrollJob.PayrollJobTypeId &&
                                p.Employee.CompanyId == _baseViewModel.CurrentCompany.InstitutionId
                        //  && p.EmployeeId == 69
                    )
                orderby p.PayrollSetupItem.Priority
                select p).ToList();
            return pitmlst;
        }

        private bool IsCurrentPayrollJobPosted()
        {
            if (_baseViewModel.CurrentPayrollJob.Status == "Posted")
            {
                MessageBox.Show("Sorry Payroll Job already posted and no further changes can be made.");
                return true;
            }

            return false;
        }

        private bool IsCurrentPayrollJobNull()
        {
            if (_baseViewModel.CurrentPayrollJob == null)
            {
                MessageBox.Show("Please Select the Current Payroll Job you want to use, then Try again.");
                return true;
            }

            return false;
        }

        private PayrollItem CreateCompanyPayrollItem(PayrollItem item, PayrollEmployeeSetup empSetupItem, InstitutionAccounts companyAccount)
        {
            try
            {


                DataLayer.PayrollItem citm = new DataLayer.PayrollItem();

                citm.ParentPayrollItemId = item.PayrollItemId;
                citm.Amount = Convert.ToDouble(empSetupItem.CompanyAmount);
                citm.Priority = item.PayrollSetupItem.Priority;
                citm.PayrollJobId = _baseViewModel.CurrentPayrollJob.PayrollJobId;
                citm.Name = item.PayrollSetupItem.CompanyLineItemDescription;
                citm.PayrollSetupItemId = item.PayrollSetupItemId;
                citm.EmployeeId = item.EmployeeId;
                citm.IncomeDeduction = item.PayrollSetupItem.IncomeDeduction;
                citm.Rate = empSetupItem.CompanyRate == null?0:(float)empSetupItem.CompanyRate;
                citm.RateRounding = empSetupItem.RateRounding;
                citm.Status = "Generated";


                //if (item.PayrollSetupItem.IncomeDeduction == true)
                //{
                //    citm.CreditAccountId = (int)item.PayrollSetupItem.CompanyAccountId;
                //    citm.DebitAccountId = item.PayrollSetupItem.PayrollItemAccountId;
                //}
                //else
                //{

                //    citm.CreditAccountId = item.PayrollSetupItem.PayrollItemAccountId;
                //    citm.DebitAccountId = (int)item.PayrollSetupItem.CompanyAccountId;
                //}
                citm.CreditAccountId = item.CreditAccountId;
                citm.DebitAccountId = companyAccount.AccountId;

                return citm;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void SetPayrollItemsAmounts()
        {
            //
            using (var ctx = new PayrollDB())
            {

                var emppayitm =
                    (from p in ctx.PayrollItems
                            .Include(x => x.PayrollSetupItem)
                            .Where(pi => pi.PayrollJobId == _baseViewModel.CurrentPayrollJob.PayrollJobId)
                        orderby p.Priority ascending
                        group p by p.Employee
                        into e
                        select new
                        {
                            emp = e.Key,
                            payitems = e.Select(p => p)
                        }).ToList();


                foreach (var emp in emppayitm)
                {
                    foreach (var payrollItem in emp.payitems)
                    {
                        payrollItem.PayrollSetupItemReference.Load();
                    }
                    CalculatePayrollAmts(emp.payitems, ctx);
                }


                BaseViewModel.SaveDatabase(ctx);
            }
        }

        public static void CalculatePayrollAmts(IEnumerable<PayrollItem> payitems, PayrollDB ctx)
        {
            try
            {
                double truebase = 0;
                double baseamount = 0;
                double amt = 0;

                var payrollItems = payitems as IList<PayrollItem> ?? payitems.ToList();
                if (!payrollItems.Any()) return;
                var plst = payrollItems.Where(p => p.ParentPayrollItemId == null ).OrderBy(p => p.Priority).ToList();
                if (plst.First().Name.Trim().ToUpper() != "Salary".ToUpper())
                {
                    MessageBox.Show("Salary is not the First item Please check Payroll Item order");
                    return;
                }

                // UpdatePayrollItemsBaseAmounts(payrollItems.ToList());

                foreach (var item in plst)
                {

                    if (item.PayrollSetupItem == null) continue;

                    
                    if (item.Name.Trim().ToUpper() == "Salary".ToUpper())
                    {
                        if (item.BaseAmount == 0)
                        {
                            truebase += item.Amount;
                        }
                        else
                        {
                            truebase = item.BaseAmount;
                        }
                    }

                    if (item.ApplyToTaxableBenefits == true)
                    {
                        baseamount = item.BaseAmount;
                    }
                    else
                    {
                        baseamount = truebase;
                    }


                   
                    
                    if (baseamount < item.PayrollSetupItem.MiniumBaseAmount)
                    {
                        ctx.PayrollItems.DeleteObject(item);
                        continue;
                    }
                   

                    amt = Convert.ToDouble((object)GetPayrollAmount(baseamount, item));

                    //set the payroll amounts   

                    item.Amount = System.Math.Abs(amt);

                    item.Status = "Amounts Processed";

                    foreach (var citm in item.PayrollItems)
                    {
                        double camt = Convert.ToDouble((object)GetPayrollAmount(baseamount, citm));
                        citm.Amount = System.Math.Abs(camt);
                        citm.Status = item.Status;
                    }
                    // update baseamount

                    //baseamount += amt;

                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public static double? GetPayrollAmount(double baseamount,  DataLayer.PayrollItem item)
        {
            double amt = 0;
            if (baseamount < item.PayrollSetupItem?.MiniumBaseAmount)
            {
                return null;
            }

            if (item.Amount != 0 && item.Rate == 0) //&& )
            {

                return DoAmountCalculation(amt, item);
            }
            else
            {

                return DoRateCalculation(baseamount, amt, item);
            }
            // return amt;
        }

        private static double DoAmountCalculation(double amt, DataLayer.PayrollItem item)
        {
            if (item.IncomeDeduction == true)
            {
                amt += item.Amount;
            }
            else
            {
                amt -= item.Amount;
            }
            return amt;
        }

        private static double? DoRateCalculation(double baseamount, double amt, DataLayer.PayrollItem item)
        {
            if (item.PayrollSetupItem.RateCeiling != null && baseamount > item.PayrollSetupItem.RateCeiling)
            {
                baseamount = (double)item.PayrollSetupItem.RateCeiling;
            }

            if (item.PayrollSetupItem.AmountFlooring != null)
            {
                baseamount -= (double)item.PayrollSetupItem.AmountFlooring;
                if (item.PayrollSetupItem.RateCeilingAmount != null)
                {
                    var maxDiff = (double)item.PayrollSetupItem.RateCeilingAmount -
                                  (double)item.PayrollSetupItem.AmountFlooring;
                    if (baseamount > maxDiff)
                    {
                        baseamount = maxDiff;
                    }
                }
            }
        

            

            if (baseamount <= 0) return null;

            item.BaseAmount = baseamount;


            if ((item.PayrollSetupItem.RateCeiling != null && item.PayrollSetupItem.RateCeilingAmount != 0) && baseamount > item.PayrollSetupItem.RateCeiling)
            {
                if (item.IncomeDeduction == true)
                {

                    amt += (double) (item.ParentPayrollItem == null?item.PayrollSetupItem.RateCeilingAmount:item.PayrollSetupItem.RateCeilingCompanyAmount);
                }
                else
                {
                    amt -= (double) (item.ParentPayrollItem == null?item.PayrollSetupItem.RateCeilingAmount : item.PayrollSetupItem.RateCeilingCompanyAmount);//item.PayrollSetupItem.RateCeilingAmount;
                }
            }
            else
            {
                if (item.Rate != 0)
                {
                    if (item.IncomeDeduction == true)
                    {
                        amt += baseamount * item.Rate;
                    }
                    else
                    {
                        amt -= baseamount * item.Rate;
                    }
                    if (item.RateRounding != null && item.RateRounding == "Up")
                    {
                        amt = Math.Round(amt, MidpointRounding.AwayFromZero);
                    }
                }
            }
            return amt;
        }
    }
}