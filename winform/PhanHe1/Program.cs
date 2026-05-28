using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using PhanHe1.Forms;

namespace PhanHe1
{
    internal static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool continueLoop = true;
            while (continueLoop)
            {
                using (var loginForm = new LoginForm())
                {
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        if (loginForm.AuthenticatedService.IsAdminSession)
                        {
                            using (var mainForm = new Form1(loginForm.AuthenticatedService))
                            {
                                if (mainForm.ShowDialog() == DialogResult.Cancel)
                                {
                                    continueLoop = true;
                                }
                                else
                                {
                                    continueLoop = false;
                                }
                            }
                        }
                        else
                        {
                            using (var userForm = new SubSystem2Form(loginForm.AuthenticatedService))
                            {
                                userForm.FormClosing += (s, e) =>
                                {
                                    if (userForm.DialogResult == DialogResult.None)
                                    {
                                        userForm.DialogResult = DialogResult.Cancel;
                                    }
                                };

                                if (userForm.ShowDialog() == DialogResult.Cancel)
                                {
                                    continueLoop = true;
                                }
                                else
                                {
                                    continueLoop = false;
                                }
                            }
                        }
                    }
                    else
                    {
                        continueLoop = false;
                    }
                }
            }
        }
    }
}
