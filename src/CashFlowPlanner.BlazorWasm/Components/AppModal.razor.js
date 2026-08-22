// Collocated with AppModal.razor, so the SDK publishes it at ./Components/AppModal.razor.js and
// nothing has to be added to wwwroot or index.html.
//
// Only the two things a Blazor component genuinely cannot do itself live here. The focus trap and
// the Escape handling are in the component, in C#, where they can be tested.

// A stack, not a flag: a confirmation opened on top of an edit dialog must not unlock the page
// when only the top one closes.
const openDialogs = [];

/**
 * Locks the page behind the dialog and remembers what had focus.
 */
export function open() {
    openDialogs.push(document.activeElement);
    document.body.classList.add('modal-open');
}

/**
 * Unlocks the page when the last dialog closes and puts focus back where it came from.
 *
 * Restoring focus is the half that is easy to leave out and the half a keyboard user notices: a
 * dialog opened from a row's Delete button and closed without it drops the user back at the top of
 * the document, several hundred rows from where they were working.
 */
export function close() {
    const previous = openDialogs.pop();

    if (openDialogs.length === 0) {
        document.body.classList.remove('modal-open');
    }

    if (previous && typeof previous.focus === 'function' && document.contains(previous)) {
        previous.focus();
    }
}
