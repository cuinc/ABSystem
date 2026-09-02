using UnityEngine;
using UnityEngine.UIElements;

public partial class LoginView : ViewBase
{
    #region Protected Methods

    protected override string UxmlPath => "UI/Documents/LoginView";

    protected override void OnBind(VisualElement root)
    {
        AutoBind(root);
    }

    protected override void OnCreated()
    {
        LoginBtn.clicked += OnLoginClicked;
        RegisterBtn.clicked += OnRegisterClicked;
        ErrorLabel.style.display = DisplayStyle.None;
        LoadingContainer.style.display = DisplayStyle.None;
    }

    protected override void OnDestroy()
    {
        LoginBtn.clicked -= OnLoginClicked;
        RegisterBtn.clicked -= OnRegisterClicked;
    }

    #endregion

    #region Private Methods

    private void OnLoginClicked()
    {
        string user = UsernameInput.value;
        string pass = PasswordInput.value;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            ShowError("用户名和密码不能为空");
            return;
        }

        SetLoading(true);
    }

    private void OnRegisterClicked()
    {
        Debug.Log("[LoginView] Register clicked");
    }

    private void ShowError(string message)
    {
        ErrorLabel.text = message;
        ErrorLabel.style.display = DisplayStyle.Flex;
    }

    private void SetLoading(bool loading)
    {
        LoadingContainer.style.display = loading ? DisplayStyle.Flex : DisplayStyle.None;
        LoginBtn.SetEnabled(!loading);
        RegisterBtn.SetEnabled(!loading);
    }

    #endregion
}
