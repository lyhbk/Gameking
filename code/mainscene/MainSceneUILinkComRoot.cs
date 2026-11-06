using UnityEngine;

public class MainSceneUILinkComRoot : MonoBehaviour
{
    [SerializeField] private ButtonLogic buttonLogic;
    [SerializeField] private InsLibCard insLibCard;

    private void Awake()
    {
        MainSceneUILink uiLink = new MainSceneUILink(buttonLogic);
        uiLink.RegisterInsLibCard(insLibCard);
        buttonLogic.GiveRequestHandler(uiLink);
        insLibCard.GiveRequestHandler(uiLink);
    }
}
