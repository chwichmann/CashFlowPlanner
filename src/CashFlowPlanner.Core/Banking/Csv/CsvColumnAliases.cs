namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// The header spellings each role answers to.
///
/// <para>
/// There is no Swiss CSV standard, and there is no Swiss CSV <i>language</i> either: the same
/// household receives German headers from one bank, French from another and English from a
/// neobank, and a bank whose web banking is set to English will emit English headers for an
/// account whose statements arrive in German. So every role carries the German, French,
/// Italian and English spellings side by side, and matching is accent- and case-insensitive so
/// that <c>Débit</c>, <c>DEBIT</c> and <c>debit</c> are one entry.
/// </para>
///
/// <para>
/// Two collisions are handled deliberately rather than accidentally. <b>Valuta</b> means the
/// value date in German and the <i>currency</i> in Italian; it is mapped to the value date,
/// because that is what it means on a Swiss statement, and the Italian sense is reached
/// through <c>divisa</c> and <c>moneta</c> instead. <b>Datum</b> alone is the booking date,
/// while <c>Valutadatum</c> is the value date - which is why the resolver prefers the longest
/// matching alias rather than the first one that appears inside the header.
/// </para>
/// </summary>
public static class CsvColumnAliases
{
    public static readonly IReadOnlyDictionary<CsvColumnRole, IReadOnlyList<string>> Default =
        new Dictionary<CsvColumnRole, IReadOnlyList<string>>
        {
            [CsvColumnRole.BookingDate] =
            [
                "buchungsdatum", "buchung", "buchungstag", "datum", "belastungsdatum",
                "transaktionsdatum", "date", "booking date", "bookingdate", "transaction date",
                "date de comptabilisation", "date comptable", "date de transaction",
                "data contabile", "data", "posted date", "date d'operation", "date operation",
                "trade date", "buchungs-datum"
            ],

            [CsvColumnRole.ValueDate] =
            [
                "valuta", "valutadatum", "valuta datum", "value date", "valuedate",
                "date de valeur", "data valuta", "wertstellung", "wertstellungsdatum",
                "valuta-datum"
            ],

            [CsvColumnRole.Amount] =
            [
                "betrag", "amount", "montant", "importo", "umsatz", "betrag chf", "betrag in chf",
                "amount chf", "montant chf", "betrag eur", "einzelbetrag", "transaktionsbetrag",
                "signed amount", "netto", "net amount", "value", "wert"
            ],

            [CsvColumnRole.Debit] =
            [
                "belastung", "belastungen", "soll", "debit", "debet", "lastschrift", "ausgang",
                "auszahlung", "abgang", "ausgaben", "aufwand", "dare", "addebito", "sortie",
                "debit chf", "belastung chf", "soll chf", "withdrawal", "money out", "paid out",
                "debit amount"
            ],

            [CsvColumnRole.Credit] =
            [
                "gutschrift", "gutschriften", "haben", "credit", "eingang", "einzahlung",
                "zugang", "einnahmen", "ertrag", "avere", "accredito", "entree", "entrée",
                "credit chf", "gutschrift chf", "haben chf", "deposit", "money in", "paid in",
                "credit amount"
            ],

            [CsvColumnRole.DebitCreditIndicator] =
            [
                "soll/haben", "soll haben", "s/h", "sh", "d/c", "dc", "debit/credit",
                "debit credit", "richtung", "vorzeichen", "sens", "segno", "cd", "credit debit",
                "haben/soll", "type", "art"
            ],

            [CsvColumnRole.Description] =
            [
                "buchungstext", "text", "beschreibung", "mitteilung", "verwendungszweck",
                "zahlungszweck", "avisierungstext", "description", "descrizione", "libelle",
                "libellé", "communication", "details", "detail", "notiz", "note", "memo",
                "narrative", "purpose", "motif", "causale", "transaktionstext", "bezeichnung",
                "subject", "titel"
            ],

            [CsvColumnRole.Counterparty] =
            [
                "empfaenger", "empfänger", "auftraggeber", "beguenstigter", "begünstigter",
                "zahlungsempfaenger", "zahlungsempfänger", "gegenkonto", "gegenpartei",
                "counterparty", "payee", "beneficiary", "beneficiaire", "bénéficiaire",
                "donneur d'ordre", "controparte", "beneficiario", "partner", "name",
                "kontoinhaber gegenpartei", "merchant", "haendler", "händler",
                "empfaenger/auftraggeber", "empfänger/auftraggeber"
            ],

            [CsvColumnRole.Reference] =
            [
                "referenz", "referenznummer", "reference", "reference number", "référence",
                "riferimento", "belegnummer", "beleg-nr", "belegnr", "transaktionsnummer",
                "transaktions-nr", "buchungsnummer", "esr referenznummer", "qr referenz",
                "end-to-end-referenz", "endtoendid", "end to end id", "zahlungsreferenz",
                "referenz nr", "transaction id", "id"
            ],

            [CsvColumnRole.Balance] =
            [
                "saldo", "kontostand", "balance", "solde", "saldo chf", "saldo in chf",
                "running balance", "laufender saldo", "neuer saldo", "endsaldo", "solde apres",
                "solde après", "saldo dopo"
            ],

            [CsvColumnRole.Currency] =
            [
                "waehrung", "währung", "currency", "devise", "divisa", "moneta", "ccy", "wkr",
                "currency code", "waehrungscode", "währungscode"
            ]
        };
}
