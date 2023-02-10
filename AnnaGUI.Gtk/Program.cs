﻿using System;
using Eto.Forms;

namespace AnnaGUI.Gtk;

static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var app = new Application(Eto.Platforms.Gtk);
        try
        {
            app.UnhandledException += ApplicationOnUnhandledException;
            app.Run(new MainForm());
        }
        catch (Exception e)
        {
            Console.WriteLine($"Unhandled Exception!\n*****Stack Trace*****\n\n{e}");
        }
        
    }
    
    private static void ApplicationOnUnhandledException(object sender, Eto.UnhandledExceptionEventArgs e)
    {
        Application.Instance.Invoke(() =>
        {
            MessageBox.Show($"Unhandled Exception!\n*****Stack Trace*****\n\n{e.ExceptionObject}", "GTK", MessageBoxType.Error);
        });
    }
}