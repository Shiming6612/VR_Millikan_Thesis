using Fusion;
using UnityEngine;
using TMPro;

public class PlayerCustomization : NetworkBehaviour
{
    public MeshRenderer BodyRenderer;
    public TextMeshPro NameText;

    [Networked, OnChangedRender(nameof(OnColorChanged))]
    public Color NetColor { get; set; }

    [Networked, OnChangedRender(nameof(OnNameChanged))]
    public NetworkString<_16> NetName { get; set; }

    public override void Spawned()
    {
        OnColorChanged();
        OnNameChanged();

        if (Object != null && Object.HasInputAuthority)
        {
            if (ClientData.Instance != null)
            {
                RPC_SetCustomization(
                    ClientData.Instance.PlayerColor,
                    ClientData.Instance.PlayerName
                );
            }
            else
            {
                Debug.LogWarning("[PlayerCustomization] ClientData.Instance is null. Using fallback values.");

                RPC_SetCustomization(Color.white, "Player");
            }
        }
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetCustomization(Color color, NetworkString<_16> name)
    {
        NetColor = color;
        NetName = name;

        OnColorChanged();
        OnNameChanged();
    }

    private void OnColorChanged()
    {
        if (BodyRenderer != null)
            BodyRenderer.material.color = NetColor;
    }

    private void OnNameChanged()
    {
        if (NameText != null)
            NameText.text = NetName.ToString();
    }
}