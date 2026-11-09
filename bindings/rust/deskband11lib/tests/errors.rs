use deskband11lib::{Error, ResultCode, TaskbarContentHost, TaskbarContentHostOptions};

#[test]
fn an_invalid_option_is_reported_with_its_name() {
    let options = TaskbarContentHostOptions {
        preferred_width: Some(0.0),
        ..Default::default()
    };
    let Err(Error::Native { result, message }) = TaskbarContentHost::new(0, &options) else {
        panic!("An invalid option must be rejected by the native library.");
    };

    assert_eq!(result, ResultCode::InvalidArgument);
    assert!(message.contains("preferred_width"), "{message}");
}

#[test]
fn a_missing_window_is_rejected() {
    let Err(Error::Native { result, .. }) =
        TaskbarContentHost::new(0, &TaskbarContentHostOptions::default())
    else {
        panic!("A missing window must be rejected by the native library.");
    };

    assert_eq!(result, ResultCode::InvalidArgument);
}
