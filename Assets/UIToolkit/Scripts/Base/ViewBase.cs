using UnityEngine;
using UnityEngine.UIElements;

public abstract class ViewBase
{
    #region Public Properties

    public VisualElement Root { get; private set; }
    public bool IsVisible => Root != null && Root.resolvedStyle.display == DisplayStyle.Flex;

    #endregion

    #region Public Methods

    public void Create(VisualElement parent)
    {
        var tree = Resources.Load<VisualTreeAsset>(UxmlPath);
        if (tree == null)
        {
            Debug.LogError($"[ViewBase] VisualTreeAsset not found: {UxmlPath}");
            return;
        }

        Root = tree.Instantiate();
        parent.Add(Root);

        OnBind(Root);
        OnCreated();
    }

    public void Show()
    {
        if (Root == null) return;
        Root.style.display = DisplayStyle.Flex;
        OnShow();
    }

    public void Hide()
    {
        if (Root == null) return;
        Root.style.display = DisplayStyle.None;
        OnHide();
    }

    public void Destroy()
    {
        OnDestroy();
        Root?.RemoveFromHierarchy();
        Root = null;
    }

    #endregion

    #region Protected Methods

    protected abstract string UxmlPath { get; }
    protected abstract void OnBind(VisualElement root);

    protected virtual void OnCreated() { }
    protected virtual void OnShow() { }
    protected virtual void OnHide() { }
    protected virtual void OnDestroy() { }

    #endregion
}
