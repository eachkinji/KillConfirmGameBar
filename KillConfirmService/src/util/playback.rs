use anyhow::{Context, Result};
use cpal::traits::{DeviceTrait, HostTrait};
use rodio::{OutputStream, OutputStreamBuilder};
use tracing::{self, info, warn};

// Function to list available host devices
pub fn list_host_devices() -> Result<()> {
    let host = cpal::default_host();
    let devices = host
        .output_devices()
        .context("unable to get output devices")?;

    for device in devices {
        let dev: rodio::Device = device;
        let dev_name = dev.name().unwrap_or_default();
        println!("{dev_name}");
    }

    Ok(())
}

pub fn default_output_device_name() -> Result<String> {
    let host = cpal::default_host();
    let device = host
        .default_output_device()
        .context("default output device is unavailable")?;
    Ok(device.name()?)
}

pub fn get_output_stream_with_name(device_name: &str) -> Result<(OutputStream, String)> {
    if device_name == "default" {
        let resolved_name = default_output_device_name()?;
        let stream = OutputStreamBuilder::open_default_stream()?;
        info!("Using default device: {}", resolved_name);
        return Ok((stream, resolved_name));
    }

    let host = cpal::default_host();
    let devices = host.output_devices()?;
    for device in devices {
        let dev: rodio::Device = device;
        let dev_name: String = dev.name()?;
        if dev_name == device_name {
            info!("Using device: {}", dev_name);
            let stream = OutputStreamBuilder::from_device(dev)?.open_stream_or_fallback()?;
            return Ok((stream, dev_name));
        }
    }

    warn!(
        "Specified device {} not found, using default output device.",
        device_name
    );
    let resolved_name = default_output_device_name()?;
    let stream = OutputStreamBuilder::open_default_stream()?;
    Ok((stream, resolved_name))
}
