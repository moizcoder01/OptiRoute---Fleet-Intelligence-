// =============================================================
//  OptiRoute  |  Program.cs
//  Entry point — launches LoginForm as the startup form.
// =============================================================
using System;
using System.Windows.Forms;
using OptiRoute.Forms;

namespace OptiRoute
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new LoginForm());
        }
    }
}