namespace Maruyama.Core
{
    /// <summary>
    /// 子どものステータス（体調・子ストレス等）を操作する抽象インターフェース。
    /// 将来 Core 側に正式な ChildStatus が実装された際に容易に差し替え可能にする。
    /// </summary>
    public interface IChildStatus
    {
        float Health { get; }
        float Stress { get; }

        void AddHealth(float delta);
        void AddStress(float delta);
    }
}
