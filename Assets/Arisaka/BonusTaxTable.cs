using UnityEngine;

namespace Arisaka
{
    /// <summary>
    /// 賞与に対する源泉徴収税額の算出率の表（令和8年分 甲欄）。
    /// 出典：平成24年3月31日財務省告示第115号 別表第三（令和7年4月30日財務省告示第122号改正）
    /// （docs/research/raw/syouyo-zeiritu.md 準拠）
    /// 
    /// 前月の社会保険料控除後の給与等の金額（①）と扶養親族等の数（0〜7人以上）から、
    /// 賞与に適用する税率（源泉徴収税率）を導き出す。
    /// </summary>
    public static class BonusTaxTable
    {
        public struct TaxRow
        {
            public float rate;
            // 扶養親族等の数 0人〜7人以上 に対応する「未満」の上限金額（単位: 千円）
            // 最後の行（45.945%）は上限なし（int.MaxValue）
            public int[] upperLimits;

            public TaxRow(float rate, params int[] limits)
            {
                this.rate = rate;
                this.upperLimits = limits;
            }
        }

        // 甲欄の全データ（単位: 千円）
        // 各セルは上限値（この金額未満であれば該当する行の率を採用）
        private static readonly TaxRow[] Rows = new TaxRow[]
        {
            //                     0人    1人    2人    3人    4人    5人    6人   7人以上
            new TaxRow(0.00000f,    82,   107,   143,   181,   218,   251,   284,   317),
            new TaxRow(0.02042f,    94,   250,   276,   300,   300,   304,   343,   383),
            new TaxRow(0.04084f,   260,   289,   321,   354,   387,   412,   438,   463),
            new TaxRow(0.06126f,   309,   346,   377,   405,   431,   457,   483,   508),
            new TaxRow(0.08168f,   342,   373,   400,   424,   452,   479,   505,   529),
            new TaxRow(0.10210f,   372,   401,   426,   452,   477,   503,   527,   552),
            new TaxRow(0.12252f,   402,   430,   457,   484,   509,   531,   553,   578),
            new TaxRow(0.14294f,   433,   463,   492,   517,   540,   564,   589,   614),
            new TaxRow(0.16336f,   520,   520,   525,   550,   577,   604,   630,   657),
            new TaxRow(0.18378f,   605,   621,   636,   651,   666,   681,   697,   708),
            new TaxRow(0.20420f,   684,   705,   728,   751,   774,   798,   821,   845),
            new TaxRow(0.22462f,   715,   739,   764,   788,   813,   838,   862,   887),
            new TaxRow(0.24504f,   752,   778,   804,   830,   856,   881,   907,   933),
            new TaxRow(0.26546f,   795,   821,   848,   876,   903,   930,   957,   985),
            new TaxRow(0.28588f,   854,   882,   910,   938,   966,   994,  1022,  1051),
            new TaxRow(0.30630f,   922,   952,   983,  1013,  1044,  1074,  1104,  1135),
            new TaxRow(0.32672f,  1318,  1342,  1367,  1391,  1416,  1440,  1464,  1489),
            new TaxRow(0.35735f,  1521,  1526,  1526,  1538,  1555,  1555,  1555,  1583),
            new TaxRow(0.38798f,  2621,  2645,  2669,  2693,  2716,  2740,  2764,  2788),
            new TaxRow(0.41861f,  3495,  3527,  3559,  3590,  3622,  3654,  3685,  3717),
            new TaxRow(0.45945f, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue)
        };

        /// <summary>
        /// 先月の給料から社会保険料を引いた金額（①、円単位）と扶養親族等の数から、
        /// 賞与に対する源泉徴収税率（算出率）を返す。
        /// </summary>
        /// <param name="amount1">先月の給料 − 社会保険料（円）</param>
        /// <param name="dependentsCount">扶養親族等の数（0人〜）</param>
        /// <returns>税率（例: 4.084% の場合は 0.04084f）</returns>
        public static float GetTaxRate(int amount1, int dependentsCount)
        {
            if (amount1 <= 0) return 0f;

            int col = Mathf.Clamp(dependentsCount, 0, 7);

            foreach (var row in Rows)
            {
                int limitInThousand = row.upperLimits[col];
                long limitInYen = limitInThousand == int.MaxValue ? long.MaxValue : (long)limitInThousand * 1000;

                // 金額が「上限未満」であれば該当行の税率を採用
                if (amount1 < limitInYen)
                {
                    return row.rate;
                }
            }

            return 0.45945f;
        }
    }
}

