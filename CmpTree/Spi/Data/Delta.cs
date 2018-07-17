using System;
using System.Collections.Generic;

namespace Spi.Data
{
    public enum DIFF_STATE
    {
        NEW,
        MODIFY,
        DELETE,
        SAMESAME
    }

    public delegate void OnCompared<T>(DIFF_STATE state, ref T a, ref T b);

    public class DiffSortedLists
    {
        public static uint Run<T>(
            IEnumerable<T> ListA,
            IEnumerable<T> ListB,
            Func<T, T, int> KeyComparer,
            Func<T, T, int> AttributeComparer,
            Action<DIFF_STATE, T, T> OnCompared,
            bool ReportSameSame,
            bool checkSortOrder)
        {
            return
                _internal_DiffSortedEnumerables<T, T, T, object>(ListA, ListB,
                KeySelector:        item => item, 
                KeyComparer:        KeyComparer,
                AttributeSelector:  item => item, 
                AttributeComparer:  AttributeComparer,
                OnCompared:         (state, a, b, ctx) => OnCompared(state, a, b), 
                checkSortOrder:     checkSortOrder,
                ReportSameSame:     ReportSameSame,
                context:            null);
        }
        public static uint DiffSortedEnumerables<T,C>(
            IEnumerable<T> ListA,
            IEnumerable<T> ListB,
            Func<T, T, int> KeyComparer,
            Func<T, T, int> AttributeComparer,
            Action<DIFF_STATE, T, T, C> OnCompared,
            bool checkSortOrder,
            C diffContext)
        {
            return
                _internal_DiffSortedEnumerables<T, T, T, C>(ListA, ListB,
                KeySelector: item => item,
                KeyComparer: KeyComparer,
                AttributeSelector: item => item,
                AttributeComparer: AttributeComparer,
                OnCompared: OnCompared,
                checkSortOrder: checkSortOrder,
                ReportSameSame: true,
                context: diffContext);
        }

        public static uint DiffSortedEnumerables<T,K,A>(
            IEnumerable<T>              ListA,
            IEnumerable<T>              ListB,
            Func<T, K>                  KeySelector,
            Func<K,K,int>               KeyComparer,
            Func<T, A>                  AttributeSelector,
            Func<A,A,int>               AttributeComparer,
            Action<DIFF_STATE, T, T>    OnCompared,
            bool                        checkSortorder)
        {
            return
                _internal_DiffSortedEnumerables<T, K, A, object>(
                    ListA, ListB, KeySelector, KeyComparer, AttributeSelector, AttributeComparer,
                    (state, a, b, ctx) => OnCompared(state, a, b), 
                    checkSortorder,
                    ReportSameSame: true,
                    context: null);
        }
        private static uint _internal_DiffSortedEnumerables<T, K, A, C> (
            IEnumerable<T>                  ListA, 
            IEnumerable<T>                  ListB,
            Func<T,K>                       KeySelector,
            Func<K,K,int>                   KeyComparer,
            Func<T,A>                       AttributeSelector,
            Func<A,A,int>                   AttributeComparer,
            Action<DIFF_STATE, T, T, C>     OnCompared,
            bool                            checkSortOrder,
            bool                            ReportSameSame,
            C                               context)
        {
            if (KeyComparer         == null) throw new ArgumentNullException(nameof(KeyComparer));
            if (KeySelector         == null) throw new ArgumentNullException(nameof(KeySelector));
            if (OnCompared          == null) throw new ArgumentNullException(nameof(OnCompared));

            using (IEnumerator<T> IterA = ListA.GetEnumerator())
            using (IEnumerator<T> IterB = ListB.GetEnumerator())
            {
                bool hasMoreA = IterA.MoveNext();
                bool hasMoreB = IterB.MoveNext();

                uint CountDifferences = 0;

                K LastKeyA = default(K);
                K LastKeyB = default(K);
                K keyA = hasMoreA ? KeySelector(IterA.Current) : default(K);
                K keyB = hasMoreB ? KeySelector(IterB.Current) : default(K);

                while (hasMoreA || hasMoreB)
                {
                    DIFF_STATE DeltaState = DIFF_STATE.SAMESAME;
                    if (hasMoreA && hasMoreB)
                    {
                        DeltaState = ItemCompareFunc(KeyComparer(keyA, keyB), IterA.Current, IterB.Current, AttributeSelector, AttributeComparer);
                        if (DeltaState != DIFF_STATE.SAMESAME || ReportSameSame )
                        {
                            OnCompared(DeltaState, IterA.Current, IterB.Current, context);
                        }
                        LastKeyA = keyA;
                        LastKeyB = keyB;
                    }
                    else if (hasMoreA && !hasMoreB)
                    {
                        DeltaState = DIFF_STATE.DELETE;
                        OnCompared(DeltaState, IterA.Current, default(T), context);
                        LastKeyA = keyA;
                        LastKeyB = default(K);
                    }
                    else if (!hasMoreA && hasMoreB)
                    {
                        DeltaState = DIFF_STATE.NEW;
                        OnCompared(DeltaState, default(T), IterB.Current, context);
                        LastKeyA = default(K);
                        LastKeyB = keyB;
                    }

                    if (DeltaState != DIFF_STATE.SAMESAME)
                    {
                        CountDifferences += 1;
                    }

                    MoveIterators(IterA, IterB, ref hasMoreA, ref hasMoreB, DeltaState);

                    if (hasMoreA)
                    {
                        keyA = KeySelector(IterA.Current);
                    }
                    if (hasMoreB)
                    {
                        keyB = KeySelector(IterB.Current);
                    }
                    if (checkSortOrder)
                    {
                        CheckSortOrderOfItems(KeyComparer, LastKeyA, keyA, 'A');
                        CheckSortOrderOfItems(KeyComparer, LastKeyB, keyB, 'B');
                    }
                }
                return CountDifferences;
            }
        }

        private static void MoveIterators<T>(IEnumerator<T> IterA, IEnumerator<T> IterB, ref bool hasMoreA, ref bool hasMoreB, DIFF_STATE DeltaState)
        {
            switch (DeltaState)
            {
                case DIFF_STATE.SAMESAME:
                case DIFF_STATE.MODIFY:
                    hasMoreA = IterA.MoveNext();
                    hasMoreB = IterB.MoveNext();
                    break;
                case DIFF_STATE.NEW:
                    hasMoreB = IterB.MoveNext();
                    break;
                case DIFF_STATE.DELETE:
                    hasMoreA = IterA.MoveNext();
                    break;
            }
        }

        private static DIFF_STATE ItemCompareFunc<T, A>(int KeyCmpResult, T itemA, T itemB, Func<T, A> attributeSelector, Func<A,A,int> attributeComparer)
        {
            if (KeyCmpResult == 0 )
            {
                if ( attributeSelector == null || attributeComparer == null )
                {
                    return DIFF_STATE.SAMESAME;
                }

                A attrA = attributeSelector(itemA);
                A attrB = attributeSelector(itemB);

                if ( attributeComparer(attrA, attrB) == 0 )
                {
                    return DIFF_STATE.SAMESAME;
                }
                else
                {
                    return DIFF_STATE.MODIFY;
                }
            }
            else
            {
                return KeyCmpResult < 0 ? DIFF_STATE.DELETE : DIFF_STATE.NEW;
            }
        }
        private static void CheckSortOrderOfItems<K>(Func<K,K,int> KeyComparer, K lastKey, K currentKey, char WhichList)
        {
            if (KeyComparer(lastKey, currentKey) > 0)
            {
                throw new ApplicationException(
                    String.Format(
                        "Sortorder not given in list [{0}]. Last item is greater than current item. Last [{1}] > [{2}] (current)",
                        WhichList,
                        lastKey.ToString(),
                        currentKey.ToString()));
            }
        }
    }
}
