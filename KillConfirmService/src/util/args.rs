use clap::Parser;
use std::ffi::OsString;

#[derive(Parser, Debug, Clone)]
#[command(version, about, long_about = None)]
pub struct Args {
    /// select output device
    #[arg(short, long, default_value = "default")]
    pub device: String,
    /// list all available audio devices
    #[arg(short, long, default_value = "false")]
    pub list_devices: bool,
    /// sound preset to use
    #[arg(short, long, default_value = "crossfire_swat_gr")]
    pub preset: String,
    /// play sound only for a specific steamid
    #[arg(long)]
    pub steamid: Option<String>,
    /// use variant of sound preset
    #[arg(long)]
    pub variant: Option<String>,

    #[arg(short, long, default_value = "1.0")]
    pub volume: f32,
    /// list all sound presets
    #[arg(short = 'L', long, default_value = "false")]
    pub list_presets: bool,

    /// close the process that owns a local TCP port, then exit
    #[arg(long)]
    pub free_port: Option<u16>,

    /// open the package runtime log folder, then exit
    #[arg(long, default_value = "false")]
    pub open_logs: bool,
}

impl Args {
    pub fn parse_runtime() -> Self {
        Self::parse_from(Self::sanitized_runtime_args())
    }

    pub fn sanitized_runtime_args() -> Vec<OsString> {
        let mut sanitized_args = Vec::new();
        let mut skip_next = false;

        for (index, arg) in std::env::args_os().enumerate() {
            if index == 0 {
                sanitized_args.push(arg);
                continue;
            }

            if skip_next {
                skip_next = false;
                continue;
            }

            let text = arg.to_string_lossy();
            if text.starts_with("/InvokerPRAID:") {
                if text == "/InvokerPRAID:" {
                    skip_next = true;
                }

                continue;
            }

            sanitized_args.push(arg);
        }

        sanitized_args
    }
}
