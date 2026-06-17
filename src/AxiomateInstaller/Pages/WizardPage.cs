using System.Windows.Controls;

namespace AxiomateInstaller.Pages;

public abstract class WizardPage : Page
{
    public virtual string HeaderSubtitleKey => "";
    public virtual bool AllowBack   => true;
    public virtual bool AllowNext   => true;
    public virtual bool AllowCancel => true;
    public virtual string NextLabelKey => "Btn_Next";

    public virtual void OnEnter(MainWindow host) { }
    public virtual void OnLeave(MainWindow host) { }
    public virtual bool Validate(MainWindow host) => true;

    public virtual int NextIndex(MainWindow host, int current) => current + 1;
    public virtual int PrevIndex(MainWindow host, int current) => current - 1;
}
