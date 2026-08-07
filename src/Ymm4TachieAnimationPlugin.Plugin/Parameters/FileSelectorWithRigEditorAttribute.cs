using System.Windows;
using System.Windows.Data;
using YukkuriMovieMaker.Commons;
using Ymm4TachieAnimationPlugin.Editor.Controls;

namespace Ymm4TachieAnimationPlugin.Plugin.Parameters;

public class FileSelectorWithRigEditorAttribute : PropertyEditorAttribute2
{
    public override FrameworkElement Create()
    {
        return new FileSelectorWithRigEditor();
    }

    public override void SetBindings(FrameworkElement control, ItemProperty[] properties)
    {
        if (control is FileSelectorWithRigEditor selector && properties.Length > 0)
        {
            var prop = properties[0];
            var binding = new Binding("Value")
            {
                Source = prop,
                Mode = BindingMode.TwoWay,
                UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            };
            selector.SetBinding(FileSelectorWithRigEditor.ValueProperty, binding);
        }
    }

    public override void ClearBindings(FrameworkElement control)
    {
        if (control is FileSelectorWithRigEditor selector)
        {
            BindingOperations.ClearBinding(selector, FileSelectorWithRigEditor.ValueProperty);
        }
    }
}
