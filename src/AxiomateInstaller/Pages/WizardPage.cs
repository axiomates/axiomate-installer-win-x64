using System.Windows.Controls;

namespace AxiomateInstaller.Pages;

public abstract class WizardPage : Page
{
    public virtual string HeaderSubtitle => "";
    public virtual bool AllowBack   => true;
    public virtual bool AllowNext   => true;
    public virtual bool AllowCancel => true;
    public virtual string NextLabel => "下一步";

    /// <summary>Called when this page becomes visible.</summary>
    public virtual void OnEnter(MainWindow host) { }

    /// <summary>Called right before navigating away. Persist state into host.Options.</summary>
    public virtual void OnLeave(MainWindow host) { }

    /// <summary>Validate before allowing forward navigation. Return false to block.</summary>
    public virtual bool Validate(MainWindow host) => true;

    public virtual int NextIndex(MainWindow host, int current) => current + 1;
    public virtual int PrevIndex(MainWindow host, int current) => current - 1;
}
