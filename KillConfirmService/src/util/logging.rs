use std::{
    env,
    fs::{self, OpenOptions},
    io::Write,
    path::PathBuf,
};

pub fn service_log(message: &str) {
    append_trace_log("service.log", message);
}

fn append_trace_log(file_name: &str, message: &str) {
    for log_dir in trace_log_directories() {
        if fs::create_dir_all(&log_dir).is_err() {
            continue;
        }

        let log_path = log_dir.join(file_name);
        let Ok(mut file) = OpenOptions::new().create(true).append(true).open(log_path) else {
            continue;
        };

        let _ = writeln!(file, "{message}");
    }
}

fn trace_log_directories() -> Vec<PathBuf> {
    let mut dirs = vec![env::temp_dir().join("KillConfirmGameBar")];

    if let Some(local_dir) = env::var_os("LOCALAPPDATA").map(PathBuf::from) {
        let local_trace_dir = local_dir.join("KillConfirmGameBar");
        if !dirs.iter().any(|dir| dir == &local_trace_dir) {
            dirs.push(local_trace_dir);
        }
    }

    dirs
}
